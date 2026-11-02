namespace Sandbox.Agents.Tools;

/// <summary>
/// Resultado que recibe el modelo de cada tool: el indicador de éxito y los datos, o el error. Al serializarse se omiten
/// los campos nulos, así que un éxito no lleva <c>error</c> y un fallo no lleva <c>data</c>.
/// </summary>
public sealed record ToolResult<T>(bool Success, T? Data, ToolError? Error);
