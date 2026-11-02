using Microsoft.Extensions.AI;
using Sandbox.Agents.Tests.Fakes;

namespace Sandbox.Agents.Tests.Learning;

/// <summary>
/// Tests de aprendizaje: fijan el comportamiento de <c>FunctionInvokingChatClient</c> (Microsoft.Extensions.AI 10.11.0)
/// en el que se apoya el borde de las tools (design D3). Si una versión nueva lo cambia, fallan aquí y no en el agente.
/// </summary>
public sealed class FunctionInvokingChatClientLearningTests
{
    [Fact]
    public async Task FunctionInvoker_WhenARequiredArgumentIsMissing_ReceivesTheArgumentExceptionBeforeTheToolRuns()
    {
        var toolRan = false;
        Exception? caught = null;
        using var model = new ScriptedChatClient(
            ScriptStep.ToolCalls(("echo", new { })),
            ScriptStep.Reply("fin"));
        var tool = AIFunctionFactory.Create((string text) => toolRan = true, "echo");
        using var loop = new FunctionInvokingChatClient(model)
        {
            FunctionInvoker = async (context, cancellationToken) =>
            {
                try
                {
                    return await context.Function.InvokeAsync(context.Arguments, cancellationToken);
                }
                catch (ArgumentException exception)
                {
                    caught = exception;
                    return "capturado";
                }
            },
        };

        await loop.GetResponseAsync("hola", new ChatOptions { Tools = [tool] }, TestContext.Current.CancellationToken);

        Assert.IsType<ArgumentException>(caught);
        Assert.False(toolRan);
        Assert.Equal("capturado", Assert.Single(model.ReceivedToolResults).Result);
    }

    [Fact]
    public async Task GetResponseAsync_WhenTheRequestIsCancelledDuringATool_PropagatesTheCancellation()
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        using var model = new ScriptedChatClient(
            ScriptStep.ToolCalls(("cancel", new { })),
            ScriptStep.Reply("no debería llegar"));
        var tool = AIFunctionFactory.Create(
            new Func<CancellationToken, string>(token =>
            {
                cancellation.Cancel();
                token.ThrowIfCancellationRequested();
                return "no debería llegar";
            }),
            "cancel");
        using var loop = new FunctionInvokingChatClient(model);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => loop.GetResponseAsync("hola", new ChatOptions { Tools = [tool] }, cancellation.Token));

        Assert.Single(model.Requests);
    }

    [Fact]
    public async Task GetResponseAsync_WhenAToolThrowsOperationCanceledWithoutCancellation_ReportsItAsAFailedCall()
    {
        using var model = new ScriptedChatClient(
            ScriptStep.ToolCalls(("cancel", new { })),
            ScriptStep.Reply("sigo"));
        var tool = AIFunctionFactory.Create(new Func<string>(() => throw new OperationCanceledException()), "cancel");
        using var loop = new FunctionInvokingChatClient(model);

        var response = await loop.GetResponseAsync(
            "hola",
            new ChatOptions { Tools = [tool] },
            TestContext.Current.CancellationToken);

        // Sin el token cancelado, el bucle no la trata como cancelación: el modelo recibe un error y continúa.
        Assert.Equal("sigo", response.Text);
        Assert.Equal(2, model.Requests.Count);
    }
}
