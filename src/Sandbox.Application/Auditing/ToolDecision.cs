namespace Sandbox.Application.Auditing;

/// <summary>Qué decidió la política sobre una petición de tool del modelo.</summary>
public enum ToolDecision
{
    /// <summary>La tool estaba permitida y se ejecutó.</summary>
    Allowed,

    /// <summary>La allow-list impidió ejecutarla.</summary>
    Blocked,
}
