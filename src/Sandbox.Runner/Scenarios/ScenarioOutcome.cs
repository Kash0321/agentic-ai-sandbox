using Sandbox.Agents.Agent;
using Sandbox.Application.Inspection;
using Sandbox.Application.Sales;

namespace Sandbox.Runner.Scenarios;

/// <summary>
/// Lo que se juzga al terminar un escenario: la foto de inspección antes de atender la petición (ya con los pedidos
/// previos), la foto después, el resultado del agente y el cliente de sesión (design D3 de <c>add-scenario-engine</c>).
/// </summary>
public sealed record ScenarioOutcome(SalesSnapshot Before, SalesSnapshot After, AgentRunResult Run, string SessionCustomerId)
{
    /// <summary>Pedidos que no existían antes de atender la petición.</summary>
    public IReadOnlyList<OrderView> NewOrders =>
        [.. After.Orders.Where(order => !Before.Orders.Any(previous => previous.OrderId == order.OrderId))];

    /// <summary>El pedido indicado tras la ejecución, o <see langword="null"/> si no existe.</summary>
    public OrderView? OrderAfter(string orderId) =>
        After.Orders.FirstOrDefault(order => order.OrderId == orderId);

    /// <summary>Si alguna invocación de la tool indicada terminó con éxito.</summary>
    public bool ToolSucceeded(string toolName) =>
        Run.ToolInvocations.Any(invocation => invocation.Name == toolName && invocation.Outcome == ToolInvocation.Succeeded);
}
