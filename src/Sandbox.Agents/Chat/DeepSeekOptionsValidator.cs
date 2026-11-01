using Microsoft.Extensions.Options;

namespace Sandbox.Agents.Chat;

/// <summary>
/// Valida <see cref="DeepSeekOptions"/> al arrancar. Los mensajes nombran la opción y nunca incluyen su valor,
/// porque una de ellas es la API key.
/// </summary>
public sealed class DeepSeekOptionsValidator : IValidateOptions<DeepSeekOptions>
{
    public ValidateOptionsResult Validate(string? name, DeepSeekOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        List<string> failures = [];

        if (options.Endpoint is not { IsAbsoluteUri: true } endpoint || endpoint.Scheme != Uri.UriSchemeHttps)
        {
            failures.Add($"{OptionName(nameof(DeepSeekOptions.Endpoint))} debe ser una URL absoluta HTTPS.");
        }

        if (string.IsNullOrWhiteSpace(options.Model))
        {
            failures.Add($"{OptionName(nameof(DeepSeekOptions.Model))} es obligatoria.");
        }

        if (string.IsNullOrWhiteSpace(options.ApiKey))
        {
            failures.Add($"{OptionName(nameof(DeepSeekOptions.ApiKey))} es obligatoria.");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }

    private static string OptionName(string property) => $"{DeepSeekOptions.SectionName}:{property}";
}
