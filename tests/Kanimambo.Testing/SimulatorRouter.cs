namespace Kanimambo.Testing;

/// <summary>
/// Primary handler for every HttpClient of the API under test: requests to a simulator's host go to that
/// simulator's in-memory server, so production clients (with their resilience handlers) are exercised as-is.
/// </summary>
internal sealed class SimulatorRouter(IReadOnlyDictionary<string, HttpMessageInvoker> routes) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
        routes.TryGetValue(request.RequestUri!.Host, out var simulator)
            ? simulator.SendAsync(request, cancellationToken)
            : throw new InvalidOperationException($"No simulator registered for host '{request.RequestUri.Host}'.");
}
