using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using PhotoBooth.Application.Events;

namespace PhotoBooth.Server.Tests;

[TestClass]
public sealed class EventsEndpointsTests
{
    [TestMethod]
    public async Task EventStream_ReturnsEventStreamContentType()
    {
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/events");
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.AreEqual("text/event-stream", response.Content.Headers.ContentType?.MediaType);
    }

    [TestMethod]
    public async Task EventStream_SendsConnectedEvent()
    {
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/events");
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        using var stream = await response.Content.ReadAsStreamAsync();
        using var reader = new StreamReader(stream);

        // Read the first SSE event (connected event)
        var eventLine = await reader.ReadLineAsync();
        var dataLine = await reader.ReadLineAsync();

        Assert.IsNotNull(eventLine);
        Assert.AreEqual("event: connected", eventLine);
        Assert.IsNotNull(dataLine);
        Assert.StartsWith("data: ", dataLine, "Data line should start with 'data: '");
        Assert.Contains("\"message\"", dataLine, "Connected event should contain a message");
    }

    [TestMethod]
    public async Task EventStream_ReceivesBroadcastedEvent()
    {
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        // Start SSE connection
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/events");
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        using var stream = await response.Content.ReadAsStreamAsync();
        using var reader = new StreamReader(stream);

        // Skip the connected event (2 lines + blank line)
        await reader.ReadLineAsync(); // event: connected
        await reader.ReadLineAsync(); // data: ...
        await reader.ReadLineAsync(); // blank line

        // Broadcast an event via the service
        var broadcaster = factory.Services.GetRequiredService<IEventBroadcaster>();
        var testEvent = new CountdownStartedEvent(3000, "test");
        await broadcaster.BroadcastAsync(testEvent);

        // Read the broadcasted event
        var eventLine = await reader.ReadLineAsync();
        var dataLine = await reader.ReadLineAsync();

        Assert.IsNotNull(eventLine);
        Assert.AreEqual("event: countdown-started", eventLine);
        Assert.IsNotNull(dataLine);
        Assert.StartsWith("data: ", dataLine);
        Assert.Contains("\"durationMs\":3000", dataLine);
    }
}
