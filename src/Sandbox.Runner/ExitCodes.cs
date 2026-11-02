namespace Sandbox.Runner;

/// <summary>Códigos de salida del Runner (spec <c>runner-host</c>).</summary>
public static class ExitCodes
{
    public const int Success = 0;

    public const int UnexpectedError = 1;

    public const int InvalidConfiguration = 2;

    public const int ModelCallFailed = 3;

    /// <summary>Un guardrail paró la ejecución del agente (spec <c>agent-guardrails</c>).</summary>
    public const int StoppedByGuardrail = 4;
}
