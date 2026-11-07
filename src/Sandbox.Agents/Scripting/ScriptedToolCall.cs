namespace Sandbox.Agents.Scripting;

/// <summary>
/// Petición de tool de un paso del guion: el nombre y los argumentos, ya convertidos a <c>JsonElement</c> como los entrega
/// el adaptador real. El identificador de la llamada lo pone el modelo con guion al responder (design D2 de
/// <c>add-scenario-engine</c>).
/// </summary>
public sealed record ScriptedToolCall(string Name, IReadOnlyDictionary<string, object?> Arguments);
