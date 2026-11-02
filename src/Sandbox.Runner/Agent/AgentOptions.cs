namespace Sandbox.Runner.Agent;

/// <summary>
/// Qué ejecuta el Runner (sección <c>Agent</c>; en la línea de comandos, <c>--scenario</c>, <c>--prompt</c> y
/// <c>--customer</c>). Sin escenario ni texto, el Runner hace el saludo de comprobación.
/// </summary>
public sealed class AgentOptions
{
    public const string SectionName = "Agent";

    /// <summary>Cliente de sesión si no se indica otro ni lo fija el escenario.</summary>
    public const string DefaultCustomerId = "CUST-001";

    public static IReadOnlyDictionary<string, string> CommandLineSwitches { get; } = new Dictionary<string, string>
    {
        ["--scenario"] = $"{SectionName}:{nameof(Scenario)}",
        ["--prompt"] = $"{SectionName}:{nameof(Prompt)}",
        ["--customer"] = $"{SectionName}:{nameof(CustomerId)}",
    };

    public string? Scenario { get; set; }

    public string? Prompt { get; set; }

    public string? CustomerId { get; set; }

    public bool RunsAgent => Scenario is not null || Prompt is not null;
}
