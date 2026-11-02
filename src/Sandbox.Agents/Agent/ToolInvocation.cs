namespace Sandbox.Agents.Agent;

/// <summary>Una invocación de tool dentro de una ejecución: su nombre y su resultado (<c>OK</c> o el código de error).</summary>
public sealed record ToolInvocation(string Name, string Outcome)
{
    public const string Succeeded = "OK";
}
