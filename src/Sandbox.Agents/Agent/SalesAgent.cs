using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sandbox.Agents.Chat;
using Sandbox.Agents.Guardrails;
using Sandbox.Agents.Tools;
using Sandbox.Application.Auditing;

namespace Sandbox.Agents.Agent;

/// <summary>
/// Agente de ventas: envía al modelo las instrucciones, la petición y las tools permitidas, y ejecuta las tools que
/// pide hasta que responde con texto o un guardrail lo para. El bucle es el <see cref="FunctionInvokingChatClient"/> de
/// Microsoft.Extensions.AI; su <c>FunctionInvoker</c> es el borde exterior de las tools (design D3 de
/// <c>add-sales-agent</c>) y aplica la allow-list y el límite de errores seguidos (D7 y D8 de
/// <c>add-agent-guardrails</c>). Las llamadas y los tokens los limita <see cref="RunBudgetChatClient"/> (D6).
/// </summary>
public sealed partial class SalesAgent(
    IChatClient chatClient,
    SalesTools tools,
    SalesSession session,
    IOptions<GuardrailOptions> guardrailOptions,
    IOptions<DeepSeekOptions> deepSeekOptions,
    AuditLog audit,
    ILogger<SalesAgent> logger)
{
    /// <summary>Longitud máxima de una petición, en caracteres (design D11). No es configurable.</summary>
    public const int MaxRequestLength = 2000;

    // Con plantilla, el scope se imprime legible en la consola y conserva RunId y CustomerId como propiedades.
    private static readonly Func<ILogger, string, string, IDisposable?> RunScope =
        LoggerMessage.DefineScope<string, string>("Ejecución {RunId} del cliente {CustomerId}");

    public async Task<AgentRunResult> RunAsync(string request, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request);
        if (request.Length > MaxRequestLength)
        {
            throw new ArgumentException(
                $"La petición tiene {request.Length} caracteres; el máximo es {MaxRequestLength}.",
                nameof(request));
        }

        // Correlación de la auditoría (design D4): los scopes siguen el flujo asíncrono, así que también los llevan los
        // eventos que emite la aplicación sin saber nada de esta ejecución.
        using var scope = RunScope(logger, Guid.NewGuid().ToString("N"), session.CustomerId);

        if (deepSeekOptions.Value.Thinking)
        {
            LogThinkingWithTools(logger);
        }

        var limits = guardrailOptions.Value;
        var run = new RunState(limits);
        var functions = tools.CreateFunctions();

        using var budget = new RunBudgetChatClient(chatClient, limits);
        using var loop = new FunctionInvokingChatClient(budget)
        {
            // La spec exige ejecutar en serie las tools de una misma respuesta. Es el valor por defecto en la 10.11.0,
            // pero se fija para que una versión futura no lo cambie en silencio.
            AllowConcurrentInvocation = false,
            // La llamada extra sin tools que hace la librería al llegar al límite (E1) es justo la que bloquea el
            // cliente de presupuesto: nunca hay más de MaxModelCalls llamadas.
            MaximumIterationsPerRequest = limits.MaxModelCalls,
            // Las tools no permitidas no se ofrecen, pero el bucle las conoce: si el modelo las pide, pasan por el
            // FunctionInvoker y reciben BLOCKED_BY_POLICY (E6, design D8).
            AdditionalTools = [.. functions.Where(function => !run.IsAllowed(function.Name))],
            FunctionInvoker = (context, token) => InvokeToolAsync(context, run, token),
        };

        List<ChatMessage> messages =
        [
            new(ChatRole.System, SystemPrompt.Text),
            new(ChatRole.User, request),
        ];
        var options = new ChatOptions { Tools = [.. functions.Where(function => run.IsAllowed(function.Name))] };

        ChatResponse? response = null;
        try
        {
            response = await loop.GetResponseAsync(messages, options, cancellationToken).ConfigureAwait(false);
        }
        catch (GuardrailStopException stop)
        {
            run.Stop(stop.Reason);
        }

        var assistantTexts = (response?.Messages ?? [])
            .Where(message => message.Role == ChatRole.Assistant && !string.IsNullOrWhiteSpace(message.Text))
            .Select(message => message.Text)
            .ToList();
        var invocations = response is null ? run.Invocations : InvocationsInRequestOrder(response, run);

        string finalAnswer;
        if (run.StopReason is AgentStopReason.Completed)
        {
            finalAnswer = assistantTexts.LastOrDefault() ?? string.Empty;
            assistantTexts = [.. assistantTexts.SkipLast(1)];
        }
        else
        {
            finalAnswer = StopMessages.For(run.StopReason);
            audit.RunStopped(run.StopReason.ToString());
        }

        foreach (var intermediate in assistantTexts)
        {
            LogIntermediateText(logger, intermediate);
        }

        return new AgentRunResult(
            finalAnswer,
            budget.Calls,
            invocations,
            budget.InputTokens,
            budget.OutputTokens,
            run.StopReason);
    }

    private async ValueTask<object?> InvokeToolAsync(
        FunctionInvocationContext context,
        RunState run,
        CancellationToken cancellationToken)
    {
        var toolName = context.Function.Name;
        var stopwatch = Stopwatch.StartNew();
        object? result;
        ToolDecision decision;

        if (!run.IsAllowed(toolName))
        {
            decision = ToolDecision.Blocked;
            result = Serialize(ToolResult.Failure<object>(ToolError.BlockedByPolicy()));
        }
        else
        {
            decision = ToolDecision.Allowed;
            try
            {
                result = await context.Function.InvokeAsync(context.Arguments, cancellationToken).ConfigureAwait(false);
            }
            catch (ArgumentException)
            {
                // El borde interior de cada tool captura lo inesperado de su cuerpo: lo que llega aquí es la asignación
                // de argumentos que no cumplen el esquema, antes de ejecutar la tool. No se reenvía el mensaje interno.
                result = Serialize(ToolResult.Failure<object>(ToolError.InvalidArguments(toolName)));
            }
        }

        var outcome = OutcomeOf(result);
        run.Record(context.CallContent.CallId, new ToolInvocation(toolName, outcome));
        audit.ToolInvoked(toolName, decision, outcome, stopwatch.ElapsedMilliseconds);

        if (run.CountOutcome(outcome))
        {
            // Corta tras esta invocación, sin volver a llamar al modelo; también se descartan las tools pendientes de
            // la misma respuesta (test de aprendizaje de Terminate, design D7).
            context.Terminate = true;
        }

        return result;
    }

    /// <summary>
    /// Tools en el orden en que las pidió el modelo (design D9). Las que no pasaron por el <c>FunctionInvoker</c> y no
    /// existen en el catálogo son nombres inventados: se registran como <c>UNKNOWN_TOOL</c>. Las del catálogo que no
    /// llegaron a ejecutarse porque un guardrail cortó la respuesta no se listan.
    /// </summary>
    private List<ToolInvocation> InvocationsInRequestOrder(ChatResponse response, RunState run)
    {
        List<ToolInvocation> ordered = [];
        foreach (var call in response.Messages.SelectMany(message => message.Contents).OfType<FunctionCallContent>())
        {
            if (run.Find(call.CallId) is { } invocation)
            {
                ordered.Add(invocation);
            }
            else if (!SalesToolNames.All.Contains(call.Name, StringComparer.Ordinal))
            {
                var name = AuditLog.TrimToolName(call.Name);
                audit.UnknownToolRequested(name);
                ordered.Add(new ToolInvocation(name, ToolErrorCodes.UnknownTool));
            }
        }

        return ordered;
    }

    private static JsonElement Serialize<T>(ToolResult<T> result) =>
        JsonSerializer.SerializeToElement(result, ToolJson.SerializerOptions);

    private static string OutcomeOf(object? result) =>
        result is JsonElement { ValueKind: JsonValueKind.Object } json
        && json.TryGetProperty("error", out var error)
        && error.TryGetProperty("code", out var code)
            ? code.GetString() ?? ToolErrorCodes.InternalError
            : ToolInvocation.Succeeded;

    [LoggerMessage(Level = LogLevel.Information, Message = "Texto intermedio del modelo: {Text}")]
    private static partial void LogIntermediateText(ILogger logger, string text);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Razonamiento activado con tools: DeepSeek documenta que reasoning_content debe reenviarse en los " +
            "turnos con tools, y esta solución no lo reenvía. En las pruebas del 2026-11-01 la API respondió igualmente.")]
    private static partial void LogThinkingWithTools(ILogger logger);

    /// <summary>Estado de una ejecución: allow-list efectiva, invocaciones, errores seguidos y motivo de parada.</summary>
    private sealed class RunState(GuardrailOptions limits)
    {
        private readonly HashSet<string> _allowed = new(limits.EffectiveAllowedTools, StringComparer.Ordinal);
        private readonly List<ToolInvocation> _invocations = [];
        private readonly Dictionary<string, ToolInvocation> _byCallId = new(StringComparer.Ordinal);
        private int _consecutiveErrors;

        public AgentStopReason StopReason { get; private set; } = AgentStopReason.Completed;

        public IReadOnlyList<ToolInvocation> Invocations => _invocations;

        public bool IsAllowed(string toolName) => _allowed.Contains(toolName);

        public void Record(string callId, ToolInvocation invocation)
        {
            _invocations.Add(invocation);
            _byCallId[callId] = invocation;
        }

        public ToolInvocation? Find(string callId) => _byCallId.GetValueOrDefault(callId);

        /// <summary>Suma un error o reinicia la cuenta; devuelve si se alcanzó el límite de errores seguidos.</summary>
        public bool CountOutcome(string outcome)
        {
            _consecutiveErrors = outcome == ToolInvocation.Succeeded ? 0 : _consecutiveErrors + 1;
            if (_consecutiveErrors < limits.MaxConsecutiveToolErrors)
            {
                return false;
            }

            Stop(AgentStopReason.ToolErrorLimit);
            return true;
        }

        public void Stop(AgentStopReason reason) => StopReason = reason;
    }
}
