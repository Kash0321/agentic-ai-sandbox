using Microsoft.Extensions.Options;
using Sandbox.Agents.Chat;

namespace Sandbox.Runner;

/// <summary>
/// Valida las opciones de DeepSeek solo cuando la ejecución va a usar el proveedor: con el modelo con guion o con la
/// suborden <c>list</c> no se exige la clave (spec <c>chat-client</c>; design D6 de <c>add-scenario-engine</c>,
/// experimento A2). Decide con la configuración y no con las opciones del agente, para no encadenar su validación.
/// </summary>
internal sealed class ProviderOptionsValidator(IValidateOptions<DeepSeekOptions> inner, Func<bool> usesProvider)
    : IValidateOptions<DeepSeekOptions>
{
    public ValidateOptionsResult Validate(string? name, DeepSeekOptions options) =>
        usesProvider() ? inner.Validate(name, options) : ValidateOptionsResult.Skip;
}
