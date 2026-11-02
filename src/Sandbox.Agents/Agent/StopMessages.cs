namespace Sandbox.Agents.Agent;

/// <summary>
/// Respuestas fijas del sistema cuando un guardrail para la ejecución (design D10). No son texto del modelo: lo que el
/// modelo dijera hasta entonces queda en el registro como texto intermedio. Avisan de que lo ya hecho se mantiene,
/// porque la parada puede llegar después de una tool que cambió estado (observado contra DeepSeek: un pedido creado y
/// la parada por presupuesto justo antes de la respuesta).
/// </summary>
internal static class StopMessages
{
    private const string Kept = "Las operaciones que ya se hubieran hecho se mantienen; revisa el resumen antes de repetir la petición.";

    public static string For(AgentStopReason reason) => reason switch
    {
        AgentStopReason.ModelCallLimit =>
            $"No he podido terminar de atender tu petición: he alcanzado el número máximo de pasos permitido. {Kept}",
        AgentStopReason.ToolErrorLimit =>
            $"No he podido terminar de atender tu petición: varias operaciones seguidas han fallado. {Kept}",
        AgentStopReason.TokenBudget =>
            $"No he podido terminar de atender tu petición: se ha agotado el presupuesto de esta conversación. {Kept}",
        _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, "La ejecución completada no tiene respuesta fija."),
    };
}
