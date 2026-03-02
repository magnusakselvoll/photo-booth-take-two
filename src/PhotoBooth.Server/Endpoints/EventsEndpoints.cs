using System.Text.Json;
using PhotoBooth.Application.Events;

namespace PhotoBooth.Server.Endpoints;

public static class EventsEndpoints
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static void MapEventsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/events", HandleSseConnection)
            .WithName("EventStream")
            .Produces(StatusCodes.Status200OK, contentType: "text/event-stream");
    }

    private static async Task HandleSseConnection(
        HttpContext context,
        IEventBroadcaster eventBroadcaster,
        IConfiguration configuration,
        CancellationToken cancellationToken)
    {
        context.Response.ContentType = "text/event-stream";
        context.Response.Headers.CacheControl = "no-cache";
        context.Response.Headers.Connection = "keep-alive";

        var heartbeatIntervalSeconds = configuration.GetValue<int?>("Watchdog:SseHeartbeatIntervalSeconds") ?? 30;
        var writeLock = new SemaphoreSlim(1, 1);

        // Send initial connection event
        await WriteEventAsync(context.Response, "connected", new { message = "Connected to event stream" }, cancellationToken);
        await context.Response.Body.FlushAsync(cancellationToken);

        var heartbeatTask = RunHeartbeatAsync(context.Response, writeLock, heartbeatIntervalSeconds, cancellationToken);

        try
        {
            await foreach (var evt in eventBroadcaster.SubscribeAsync(cancellationToken))
            {
                await writeLock.WaitAsync(cancellationToken);
                try
                {
                    await WriteEventAsync(context.Response, evt.EventType, evt, cancellationToken);
                    await context.Response.Body.FlushAsync(cancellationToken);
                }
                finally
                {
                    writeLock.Release();
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Client disconnected, this is expected
        }

        await heartbeatTask;
    }

    private static async Task RunHeartbeatAsync(
        HttpResponse response,
        SemaphoreSlim writeLock,
        int intervalSeconds,
        CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), cancellationToken);

                await writeLock.WaitAsync(cancellationToken);
                try
                {
                    await response.WriteAsync(":heartbeat\n\n", cancellationToken);
                    await response.Body.FlushAsync(cancellationToken);
                }
                finally
                {
                    writeLock.Release();
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when cancellation is requested
        }
    }

    private static async Task WriteEventAsync(HttpResponse response, string eventType, object data, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(data, JsonOptions);
        await response.WriteAsync($"event: {eventType}\n", cancellationToken);
        await response.WriteAsync($"data: {json}\n\n", cancellationToken);
    }
}
