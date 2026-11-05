using Microsoft.Extensions.AI;
using Sandbox.Agents.Tests.Fakes;

namespace Sandbox.Agents.Tests.Learning;

/// <summary>
/// Tests de aprendizaje: fijan el comportamiento de <c>FunctionInvokingChatClient</c> (Microsoft.Extensions.AI 10.11.0)
/// en el que se apoyan los guardrails (experimentos E1, E2, E4, E5 y E6 del design de <c>add-agent-guardrails</c>). Si
/// una versión nueva lo cambia, fallan aquí y no en el agente.
/// </summary>
public sealed class GuardrailLearningTests
{
    private static readonly AIFunction Ping = AIFunctionFactory.Create(() => "ok", "ping");

    [Fact]
    public async Task MaximumIterationsPerRequest_WhenReached_MakesOneMoreCallWithoutTools()
    {
        using var model = new ScriptedChatClient(
            ScriptStep.ToolCalls(("ping", new { })),
            ScriptStep.ToolCalls(("ping", new { })),
            ScriptStep.Reply("fin"));
        using var loop = new FunctionInvokingChatClient(model) { MaximumIterationsPerRequest = 2 };

        await loop.GetResponseAsync("hola", new ChatOptions { Tools = [Ping] }, TestContext.Current.CancellationToken);

        // E1: con un límite de 2, el modelo recibe 3 llamadas, y la última ya no ofrece tools.
        Assert.Equal(3, model.Requests.Count);
        Assert.NotEmpty(model.RequestOptions[1]?.Tools ?? []);
        Assert.Empty(model.RequestOptions[2]?.Tools ?? []);
    }

    [Fact]
    public async Task MaximumConsecutiveErrorsPerRequest_WhenToolsReturnErrorsWithoutThrowing_DoesNotStopTheLoop()
    {
        using var model = new ScriptedChatClient(
            ScriptStep.ToolCalls(("fail", new { })),
            ScriptStep.ToolCalls(("fail", new { })),
            ScriptStep.ToolCalls(("fail", new { })),
            ScriptStep.Reply("fin"));
        var fail = AIFunctionFactory.Create(() => new { error = new { code = "ORDER_NOT_FOUND" } }, "fail");
        using var loop = new FunctionInvokingChatClient(model) { MaximumConsecutiveErrorsPerRequest = 1 };

        var response = await loop.GetResponseAsync("hola", new ChatOptions { Tools = [fail] }, TestContext.Current.CancellationToken);

        // E2: el límite solo cuenta excepciones; un error devuelto como dato no cuenta.
        Assert.Equal("fin", response.Text);
        Assert.Equal(4, model.Requests.Count);
    }

    [Fact]
    public async Task Terminate_OnTheFirstOfTwoCallsInOneResponse_RunsNeitherTheSecondCallNorAnotherModelCall()
    {
        List<string> invoked = [];
        using var model = new ScriptedChatClient(
            ScriptStep.ToolCalls(("first", new { }), ("second", new { })),
            ScriptStep.Reply("no debería llegar"));
        using var loop = new FunctionInvokingChatClient(model)
        {
            FunctionInvoker = (context, cancellationToken) =>
            {
                invoked.Add(context.Function.Name);
                context.Terminate = context.Function.Name == "first";
                return context.Function.InvokeAsync(context.Arguments, cancellationToken);
            },
        };
        var tools = new ChatOptions
        {
            Tools = [AIFunctionFactory.Create(() => "1", "first"), AIFunctionFactory.Create(() => "2", "second")],
        };

        await loop.GetResponseAsync("hola", tools, TestContext.Current.CancellationToken);

        // Pregunta abierta del design: Terminate corta también las tools pendientes de la misma respuesta.
        Assert.Equal(["first"], invoked);
        Assert.Single(model.Requests);
    }

    [Fact]
    public async Task UnknownTool_WhenRequested_DoesNotReachTheFunctionInvoker()
    {
        List<string> invoked = [];
        using var model = new ScriptedChatClient(
            ScriptStep.ToolCalls(("ship_order", new { })),
            ScriptStep.Reply("fin"));
        using var loop = new FunctionInvokingChatClient(model)
        {
            FunctionInvoker = (context, cancellationToken) =>
            {
                invoked.Add(context.Function.Name);
                return context.Function.InvokeAsync(context.Arguments, cancellationToken);
            },
        };

        var response = await loop.GetResponseAsync("hola", new ChatOptions { Tools = [Ping] }, TestContext.Current.CancellationToken);

        // E5: la librería responde al modelo con su propio texto y continúa, sin pasar por nuestro código.
        Assert.Empty(invoked);
        Assert.Equal("fin", response.Text);
        Assert.Contains("ship_order", Assert.Single(model.ReceivedToolResults).Result?.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AdditionalTools_WhenRequestedButNotOffered_ReachTheFunctionInvoker()
    {
        List<string> invoked = [];
        using var model = new ScriptedChatClient(
            ScriptStep.ToolCalls(("hidden", new { })),
            ScriptStep.Reply("fin"));
        using var loop = new FunctionInvokingChatClient(model)
        {
            AdditionalTools = [AIFunctionFactory.Create(() => "oculta", "hidden")],
            FunctionInvoker = (context, cancellationToken) =>
            {
                invoked.Add($"{context.Function.Name}#{context.CallContent.CallId}");
                return ValueTask.FromResult<object?>("bloqueada");
            },
        };

        await loop.GetResponseAsync("hola", new ChatOptions { Tools = [Ping] }, TestContext.Current.CancellationToken);

        // E6: no se ofrece al modelo, pero si la pide pasa por el FunctionInvoker, con su CallId.
        Assert.Equal(["ping"], model.RequestOptions[0]?.Tools?.Select(tool => tool.Name));
        Assert.StartsWith("hidden#call_", Assert.Single(invoked), StringComparison.Ordinal);
        Assert.Equal("bloqueada", Assert.Single(model.ReceivedToolResults).Result);
    }
}
