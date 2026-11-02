using Microsoft.Extensions.Logging;

namespace Sandbox.Application.Auditing;

/// <summary>
/// Eventos de auditoría, en una categoría de registro propia y con identificadores estables (design D4). La correlación
/// (identificador de ejecución y cliente de sesión) llega por el <i>scope</i> que abre quien ejecuta el agente; por eso
/// ningún evento la recibe como argumento. Ningún evento lleva argumentos de tools, datos de resultados, el texto de
/// la petición ni secretos.
/// </summary>
public sealed partial class AuditLog(ILogger logger)
{
    /// <summary>Categoría de registro de la auditoría, separada de la de los registros operativos.</summary>
    public const string Category = "Sandbox.Audit";

    /// <summary>Motivo de una denegación por pedido de otro cliente.</summary>
    public const string ForeignOrderReason = "pedido de otro cliente";

    /// <summary>Longitud máxima de un nombre de tool pedido por el modelo en la auditoría: lo controla el modelo.</summary>
    public const int MaxToolNameLength = 64;

    public static AuditLog Create(ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(loggerFactory);
        return new AuditLog(loggerFactory.CreateLogger(Category));
    }

    /// <summary>Recorta un nombre controlado por el modelo para que no inunde la auditoría.</summary>
    public static string TrimToolName(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return name.Length <= MaxToolNameLength ? name : name[..MaxToolNameLength];
    }

    [LoggerMessage(
        EventId = 5001,
        EventName = "ToolInvoked",
        Level = LogLevel.Information,
        Message = "Tool {Tool}: decisión {Decision}, resultado {Outcome} en {ElapsedMs} ms")]
    public partial void ToolInvoked(string tool, ToolDecision decision, string outcome, long elapsedMs);

    [LoggerMessage(
        EventId = 5002,
        EventName = "UnknownToolRequested",
        Level = LogLevel.Warning,
        Message = "El modelo pidió una tool que no existe: {Tool}")]
    public partial void UnknownToolRequested(string tool);

    [LoggerMessage(
        EventId = 5003,
        EventName = "RunStopped",
        Level = LogLevel.Warning,
        Message = "Ejecución parada por un guardrail: {Reason}")]
    public partial void RunStopped(string reason);

    [LoggerMessage(
        EventId = 5004,
        EventName = "AccessDenied",
        Level = LogLevel.Warning,
        Message = "Acceso denegado en {Operation} al pedido {OrderId} para el cliente {CustomerId}: {Reason}")]
    public partial void AccessDenied(string operation, string orderId, string customerId, string reason);
}
