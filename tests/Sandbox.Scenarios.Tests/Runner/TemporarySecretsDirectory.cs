namespace Sandbox.Scenarios.Tests.Runner;

/// <summary>Directorio temporal que hace de <c>/run/secrets</c> en los tests.</summary>
internal sealed class TemporarySecretsDirectory : IDisposable
{
    public TemporarySecretsDirectory()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"sandbox-secrets-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    /// <summary>Ruta que no existe, para el caso "sin directorio de secretos".</summary>
    public static string Missing() =>
        System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"sandbox-secrets-missing-{Guid.NewGuid():N}");

    public void Write(string fileName, string content) => File.WriteAllText(System.IO.Path.Combine(Path, fileName), content);

    public void Dispose() => Directory.Delete(Path, recursive: true);
}
