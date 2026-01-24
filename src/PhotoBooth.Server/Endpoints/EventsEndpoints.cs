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
        CancellationToken cancellationToken)
    {
        context.Response.ContentType = "text/event-stream";
        context.Response.Headers.CacheControl = "no-cache";
        context.Response.Headers.Connection = "keep-alive";

        // Send initial connection event
        await WriteEventAsync(context.Response, "connected", new { message = "Connected to event stream" }, cancellationToken);
        await context.Response.Body.FlushAsync(cancellationToken);

        try
        {
            await foreach (var evt in eventBroadcaster.SubscribeAsync(cancellationToken))
            {
                await WriteEventAsync(context.Response, evt.EventType, evt, cancellationToken);
                await context.Response.Body.FlushAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Client disconnected, this is expected
        }
    }

    private static async Task WriteEventAsync(HttpResponse response, string eventType, object data, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(data, JsonOptions);
        await response.WriteAsync($"event: {eventType}\n", cancellationToken);
        await response.WriteAsync($"data: {json}\n\n", cancellationToken);
    }
}
