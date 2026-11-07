namespace Sandbox.Runner.Agent;

/// <summary>
/// Qué ejecuta el Runner (sección <c>Agent</c>; en la línea de comandos, <c>--scenario</c>, <c>--prompt</c>,
/// <c>--customer</c>, <c>--model</c> y la suborden posicional <c>list</c>). Sin escenario, texto ni suborden, el Runner
/// hace el saludo de comprobación.
/// </summary>
public sealed class AgentOptions
{
    public const string SectionName = "Agent";

    /// <summary>Cliente de sesión si no se indica otro ni lo fija el escenario.</summary>
    public const string DefaultCustomerId = "CUST-001";

    /// <summary>Respuestas del proveedor configurado en <c>DeepSeek</c>; es el valor por defecto.</summary>
    public const string DeepSeekModel = "deepseek";

    /// <summary>Respuestas del guion del escenario (spec <c>agent-scenarios</c>).</summary>
    public const string ScriptedModel = "scripted";

    /// <summary>Suborden que lista los escenarios.</summary>
    public const string ListCommand = "list";

    public static IReadOnlyDictionary<string, string> CommandLineSwitches { get; } = new Dictionary<string, string>
    {
        ["--scenario"] = $"{SectionName}:{nameof(Scenario)}",
        ["--prompt"] = $"{SectionName}:{nameof(Prompt)}",
        ["--customer"] = $"{SectionName}:{nameof(CustomerId)}",
        ["--model"] = $"{SectionName}:{nameof(ModelSource)}",
    };

    public string? Scenario { get; set; }

    public string? Prompt { get; set; }

    public string? CustomerId { get; set; }

    /// <summary>De dónde salen las respuestas del modelo: <see cref="DeepSeekModel"/> (por defecto) o <see cref="ScriptedModel"/>.</summary>
    public string? ModelSource { get; set; }

    /// <summary>Argumentos posicionales, separados por espacios; solo <see cref="ListCommand"/> es válido.</summary>
    public string? Command { get; set; }

    public bool RunsAgent => Scenario is not null || Prompt is not null;

    public bool ListsScenarios => IsList(Command);

    public bool UsesScriptedModel => IsScripted(ModelSource);

    internal static bool IsList(string? command) => string.Equals(command, ListCommand, StringComparison.Ordinal);

    internal static bool IsScripted(string? modelSource) =>
        string.Equals(modelSource?.Trim(), ScriptedModel, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Argumentos posicionales de <paramref name="args"/>: los que no empiezan por <c>-</c> ni <c>/</c>, no contienen
    /// <c>=</c> y no son el valor de una opción anterior sin <c>=</c>. La configuración los ignora sin error (design D6
    /// de <c>add-scenario-engine</c>, experimento A4).
    /// </summary>
    internal static IReadOnlyList<string> PositionalArguments(IReadOnlyList<string> args)
    {
        List<string> positional = [];
        for (var index = 0; index < args.Count; index++)
        {
            var argument = args[index];
            if (IsSwitch(argument))
            {
                if (!argument.Contains('=', StringComparison.Ordinal))
                {
                    index++;
                }
            }
            else if (!argument.Contains('=', StringComparison.Ordinal))
            {
                positional.Add(argument);
            }
        }

        return positional;
    }

    private static bool IsSwitch(string argument) =>
        argument.StartsWith('-') || argument.StartsWith('/');
}
