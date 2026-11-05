using System.Text.Json;
using Sandbox.Agents.Approval;
using Sandbox.Agents.Tools;

namespace Sandbox.Runner.Agent;

/// <summary>
/// Aprobador de consola del Runner (spec <c>tool-approval</c>, design D5): muestra la acción exacta por la salida de
/// errores y pregunta <c>[s/N]</c>. Sin consola interactiva deniega sin leer nada (<i>fail closed</i>). La entrada, la
/// salida y la comprobación de consola se inyectan para probarlo sin teclado.
/// </summary>
public sealed class ConsoleToolApprover(TextReader input, TextWriter error, Func<bool> isInteractive) : IToolApprover
{
    public const string ConsoleApprover = "Console";

    public const string NonInteractiveApprover = "NonInteractive";

    public const string NonInteractiveReason = "sin consola interactiva";

    public const string RejectedReason = "el cliente no lo aprobó";

    // Respuestas que aprueban, sin distinguir mayúsculas; "sí" es lo que escribe un hispanohablante.
    private static readonly string[] Yes = ["s", "si", "sí"];

    /// <summary>Aprobador sobre la consola del proceso: interactiva si la entrada estándar no está redirigida.</summary>
    public static ConsoleToolApprover ForProcessConsole() =>
        new(Console.In, Console.Error, () => !Console.IsInputRedirected);

    public async Task<ToolApprovalDecision> DecideAsync(ToolApprovalRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!isInteractive())
        {
            return ToolApprovalDecision.Deny(NonInteractiveApprover, NonInteractiveReason);
        }

        // En JSON, un carácter de control enviado por el modelo (por ejemplo, ESC) se muestra escapado y no llega a
        // la terminal, donde podría iniciar una secuencia que cambie lo que ve el humano.
        var arguments = JsonSerializer.Serialize(request.Arguments, ToolJson.SerializerOptions);
        await error.WriteLineAsync($"El agente quiere ejecutar {request.ToolName} con {arguments}. ¿Lo apruebas? [s/N]")
            .ConfigureAwait(false);
        await error.FlushAsync(cancellationToken).ConfigureAwait(false);

        var answer = await input.ReadLineAsync(cancellationToken).ConfigureAwait(false);
        return IsYes(answer)
            ? ToolApprovalDecision.Approve(ConsoleApprover)
            : ToolApprovalDecision.Deny(ConsoleApprover, RejectedReason);
    }

    private static bool IsYes(string? answer) =>
        answer?.Trim() is { } trimmed && Yes.Any(yes => trimmed.Equals(yes, StringComparison.OrdinalIgnoreCase));
}
