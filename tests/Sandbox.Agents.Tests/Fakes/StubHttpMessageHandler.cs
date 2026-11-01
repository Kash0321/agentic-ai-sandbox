using System.Net;
using System.Text;

namespace Sandbox.Agents.Tests.Fakes;

/// <summary>
/// Transporte HTTP simulado para probar el cliente real sin red. Devuelve una respuesta grabada y guarda la petición.
/// </summary>
internal sealed class StubHttpMessageHandler(Func<CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
{
    public Uri? RequestUri { get; private set; }

    public string? RequestBody { get; private set; }

    public string? Authorization { get; private set; }

    public static StubHttpMessageHandler Returning(HttpStatusCode status, string json) =>
        new(_ => Task.FromResult(new HttpResponseMessage(status)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        }));

    public static StubHttpMessageHandler Throwing(Exception exception) =>
        new(_ => Task.FromException<HttpResponseMessage>(exception));

    public static StubHttpMessageHandler NeverResponding() =>
        new(async cancellationToken =>
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            throw new InvalidOperationException("No debería llegar aquí.");
        });

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        RequestUri = request.RequestUri;
        Authorization = request.Headers.TryGetValues("Authorization", out var values) ? values.Single() : null;
        RequestBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);

        return await respond(cancellationToken);
    }
}
