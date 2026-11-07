using System.Diagnostics;
using Microsoft.Extensions.AI;
using Sandbox.Agents.Tests.Fakes;

namespace Sandbox.Agents.Tests.Learning;

/// <summary>
/// Tests de aprendizaje: fijan cómo emiten spans <c>OpenTelemetryChatClient</c> y <c>FunctionInvokingChatClient</c>
/// (Microsoft.Extensions.AI 10.11.0) en la forma del agente (experimentos A2a, A2b, A3 y A4 del design de
/// <c>add-agent-observability</c>, con el ajuste del apply sobre el nombre del span ambiente). Si una versión nueva lo
/// cambia, fallan aquí y no en el agente.
/// </summary>
[Collection(nameof(TelemetrySerialGroup))]
public sealed class TelemetryLearningTests
{
    private const string ChatSource = "TelemetryLearningTests.Chat";
    private const string RootSource = "TelemetryLearningTests.Root";
    private const string AgentRoot = "invoke_agent sales-agent";
    private static readonly object OrderOne = new { orderId = "ORD-0001" };

    [Fact]
    public async Task ToolLoop_WithoutAmbientSpan_OpensAnOrchestrateRootPerCallAndSplitsTheTraceAtTheApprovalPause()
    {
        using var recorder = new SpanRecorder(ChatSource, RootSource);

        await new Probe(rootName: null).RunWithApprovalAsync();

        var orchestrations = recorder.Named("orchestrate_tools");
        Assert.Equal(2, orchestrations.Count);
        Assert.All(orchestrations, span => Assert.Equal(default, span.ParentSpanId));
        Assert.NotEqual(orchestrations[0].TraceId, orchestrations[1].TraceId);
    }

    [Fact]
    public async Task ToolLoop_UnderAnAmbientSpanWithAnotherName_KeepsAnOrchestrateSpanPerCallInsideTheSameTrace()
    {
        using var recorder = new SpanRecorder(ChatSource, RootSource);

        await new Probe("root").RunWithApprovalAsync();

        var root = Assert.Single(recorder.Named("root"));
        var orchestrations = recorder.Named("orchestrate_tools");
        Assert.Equal(2, orchestrations.Count);
        Assert.All(orchestrations, span => Assert.Equal(root.SpanId, span.ParentSpanId));
    }

    [Fact]
    public async Task ToolLoop_UnderAnInvokeAgentSpan_HangsModelAndToolSpansFromItWithoutOrchestrateSpans()
    {
        using var recorder = new SpanRecorder(ChatSource, RootSource);

        await new Probe(AgentRoot).RunWithApprovalAsync();

        var root = Assert.Single(recorder.Named(AgentRoot));
        Assert.Empty(recorder.Named("orchestrate_tools"));
        var children = recorder.Named("chat").Concat(recorder.Named("execute_tool")).ToList();
        Assert.Equal(5, children.Count);
        Assert.All(children, span => Assert.Equal(root.SpanId, span.ParentSpanId));
        // Los spans de tools toman la fuente del span ambiente, no la del cliente de chat.
        Assert.All(recorder.Named("execute_tool"), span => Assert.Equal(RootSource, span.Source.Name));
    }

    [Fact]
    public async Task ToolLoop_UnderAnotherNameWithTheInvokeAgentOperationTag_StillOpensOrchestrateSpans()
    {
        using var recorder = new SpanRecorder(ChatSource, RootSource);

        await new Probe("root", tagAsAgent: true).RunWithApprovalAsync();

        // Lo decide el nombre del span ambiente, no su atributo gen_ai.operation.name.
        Assert.Equal(2, recorder.Named("orchestrate_tools").Count);
    }

    [Fact]
    public async Task ChatSpan_WhenTheInnerClientThrows_CopiesTheExceptionMessageIntoTheStatus()
    {
        using var recorder = new SpanRecorder(ChatSource);
        using var client = new ThrowingChatClient("Authentication Fails, Your api key: ****SECR is invalid")
            .AsBuilder()
            .UseOpenTelemetry(sourceName: ChatSource)
            .Build();

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            client.GetResponseAsync("hola", cancellationToken: TestContext.Current.CancellationToken));

        var span = Assert.Single(recorder.Named("chat"));
        Assert.Equal(ActivityStatusCode.Error, span.Status);
        Assert.Contains("****SECR", span.StatusDescription, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ToolSpan_WithSensitiveDataAndNoAmbientSpan_RecordsTheArguments()
    {
        using var recorder = new SpanRecorder(ChatSource, RootSource);

        await new Probe(rootName: null, sensitive: true).RunWithApprovalAsync();

        var toolSpans = recorder.Named("execute_tool");
        Assert.NotEmpty(toolSpans);
        Assert.All(toolSpans, span => Assert.NotNull(span.GetTagItem("gen_ai.tool.call.arguments")));
    }

    [Fact]
    public async Task ToolSpan_WithSensitiveDataUnderAnInvokeAgentSpan_DoesNotRecordTheArguments()
    {
        using var recorder = new SpanRecorder(ChatSource, RootSource);

        await new Probe(AgentRoot, sensitive: true).RunWithApprovalAsync();

        var toolSpans = recorder.Named("execute_tool");
        Assert.NotEmpty(toolSpans);
        Assert.All(toolSpans, span => Assert.Null(span.GetTagItem("gen_ai.tool.call.arguments")));
        // El contenido sí viaja en los mensajes de las llamadas al modelo.
        Assert.Contains(recorder.Named("chat"), span => span.GetTagItem("gen_ai.output.messages") is string output
            && output.Contains("ORD-0001", StringComparison.Ordinal));
    }

    /// <summary>
    /// Modelo que pide <c>get_order</c>, después <c>cancel_order</c> (que requiere aprobación) y luego responde; el
    /// bucle se pausa en la aprobación y se reanuda aprobando, como en el agente. Sin <paramref name="rootName"/>, no
    /// hay span ambiente.
    /// </summary>
    private sealed class Probe(string? rootName, bool sensitive = false, bool tagAsAgent = false)
    {
        public async Task RunWithApprovalAsync()
        {
            var token = TestContext.Current.CancellationToken;
            var model = new ScriptedChatClient(
                ScriptStep.ToolCalls(("get_order", OrderOne)),
                ScriptStep.ToolCalls(("cancel_order", OrderOne)),
                ScriptStep.Reply("Cancelado"));
            using var traced = model.AsBuilder()
                .UseOpenTelemetry(sourceName: ChatSource, configure: client => client.EnableSensitiveData = sensitive)
                .Build();
            using var loop = new FunctionInvokingChatClient(traced)
            {
                FunctionInvoker = (context, cancellationToken) => context.Function.InvokeAsync(context.Arguments, cancellationToken),
            };
            var options = new ChatOptions
            {
                Tools =
                [
                    AIFunctionFactory.Create((string orderId) => $"pedido {orderId}", "get_order"),
                    new ApprovalRequiredAIFunction(AIFunctionFactory.Create((string orderId) => $"cancelado {orderId}", "cancel_order")),
                ],
            };
            List<ChatMessage> history = [new(ChatRole.User, "cancela ORD-0001")];

            using var rootSource = new ActivitySource(RootSource);
            using var root = rootName is null ? null : rootSource.StartActivity(rootName);
            if (tagAsAgent)
            {
                root?.SetTag("gen_ai.operation.name", "invoke_agent");
            }

            var first = await loop.GetResponseAsync(history, options, token);
            var requests = first.Messages.SelectMany(message => message.Contents).OfType<ToolApprovalRequestContent>().ToList();
            history.AddRange(first.Messages);
            history.Add(new ChatMessage(ChatRole.User, [.. requests.Select(request => (AIContent)request.CreateResponse(approved: true))]));
            await loop.GetResponseAsync(history, options, token);
        }
    }

    private sealed class ThrowingChatClient(string message) : IChatClient
    {
        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default) =>
            Task.FromException<ChatResponse>(new HttpRequestException(message));

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose()
        {
        }
    }
}
