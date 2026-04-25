namespace PhotoBooth.Server.Tests.LoadTesting;

// Adds a simulated round-trip delay to every HTTP request to approximate real network conditions.
internal sealed class LatencyHandler(TimeSpan latency) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        await Task.Delay(latency / 2, cancellationToken);
        var response = await base.SendAsync(request, cancellationToken);
        await Task.Delay(latency / 2, cancellationToken);
        return response;
    }
}
