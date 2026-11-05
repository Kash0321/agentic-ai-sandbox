using Sandbox.Agents.Tools;

namespace Sandbox.Agents.Guardrails;

/// <summary>
/// Límites y allow-list de cada ejecución del agente (sección <c>Guardrails</c>, spec <c>agent-guardrails</c>). Los
/// valores por defecto cubren con margen los escenarios medidos en la fase 4: 3 y 4 llamadas, unos 4.500 tokens.
/// </summary>
public sealed class GuardrailOptions
{
    public const string SectionName = "Guardrails";

    public const int DefaultMaxModelCalls = 10;

    public const int DefaultMaxConsecutiveToolErrors = 3;

    public const int DefaultTokenBudget = 20_000;

    /// <summary>Llamadas al modelo por ejecución, incluida la primera.</summary>
    public int MaxModelCalls { get; set; } = DefaultMaxModelCalls;

    /// <summary>Invocaciones de tools seguidas que pueden terminar con error antes de parar la ejecución.</summary>
    public int MaxConsecutiveToolErrors { get; set; } = DefaultMaxConsecutiveToolErrors;

    /// <summary>Tokens de entrada y de salida que pueden acumular las llamadas de una ejecución.</summary>
    public int TokenBudget { get; set; } = DefaultTokenBudget;

    /// <summary>
    /// Tools permitidas; <see langword="null"/> significa todas. No se inicializa con las seis porque el <i>binder</i>
    /// de configuración añadiría las configuradas a las de por defecto en lugar de sustituirlas (design D5).
    /// </summary>
    public IList<string>? AllowedTools { get; set; }

    /// <summary>Las tools permitidas en la práctica: las configuradas o, si no hay, todo el catálogo.</summary>
    public IReadOnlyList<string> EffectiveAllowedTools => AllowedTools is null ? SalesToolNames.All : [.. AllowedTools];
}
