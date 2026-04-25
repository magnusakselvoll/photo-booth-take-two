using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NBomber.Contracts;
using NBomber.Contracts.Stats;
using NBomber.CSharp;
using PhotoBooth.Application.DTOs;
using PhotoBooth.Application.Services;
using PhotoBooth.Domain.Entities;
using PhotoBooth.Domain.Interfaces;
using PhotoBooth.Infrastructure.Imaging;
using PhotoBooth.Infrastructure.Storage;
using PhotoBooth.Server.Tests.LoadTesting;

namespace PhotoBooth.Server.Tests;

// Load test for the guest-facing photo gallery endpoints (issue #223).
//
// Uses NBomber with an in-process TestServer (WebApplicationFactory default) rather than real
// Kestrel. This means Kestrel connection-queuing is not under load, but all server-side work
// — FileSystemPhotoRepository disk reads, OpenCvImageResizer thumbnail lookups, JSON
// serialisation, security headers — is exercised identically. The result accurately
// characterises CPU, memory, and I/O resource consumption.
//
// NETWORK SIMULATION
//   Latency: ~100ms RTT added by LatencyHandler (50ms before send + 50ms after receive).
//   Bandwidth: not simulated in-process; see analytic ceiling in test output.
//
// HOW TO RUN
//   dotnet test tests/PhotoBooth.Server.Tests --filter "FullyQualifiedName~GuestLoadTests" --logger "console;verbosity=detailed"
//
// ENVIRONMENT VARIABLES
//   LOADTEST_TARGET_USERS  – total virtual users across all scenarios (default 50)
//   LOADTEST_PHOTO_COUNT   – photos to seed into temp storage (default 50)
//   LOADTEST_DURATION_MIN  – hold-phase duration in minutes (default 3)
[TestClass]
[TestCategory("Integration")]
public sealed class GuestLoadTests
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    private static SeededPhotos _seeded = null!;
    private static WebApplicationFactory<Program> _factory = null!;

    [ClassInitialize]
    public static void ClassInit(TestContext testContext)
    {
        var photoCount = int.Parse(
            Environment.GetEnvironmentVariable("LOADTEST_PHOTO_COUNT") ?? "50");

        testContext.WriteLine($"Seeding {photoCount} photos...");
        _seeded = PhotoSeeder.Seed(photoCount);
        testContext.WriteLine($"Photos seeded to {_seeded.BasePath}");

        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((_, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        // Camera and trigger overrides are read at DI-registration time so
                        // ConfigureAppConfiguration is early enough for these.
                        ["Camera:Provider"] = "mock",
                        ["Trigger:RestrictToLocalhost"] = "false",
                        ["Watchdog:ServerInactivityMinutes"] = "0"
                    });
                });
                builder.ConfigureServices(services =>
                {
                    // Serilog is registered on IHostBuilder (not IWebHostBuilder), so
                    // ConfigureLogging on the web host doesn't reach it. Remove its
                    // ILoggerFactory and replace with a Warning-level one so Info-level
                    // request logs don't flood the load-test output.
                    foreach (var d in services.Where(s => s.ServiceType == typeof(ILoggerFactory)).ToList())
                        services.Remove(d);
                    services.AddLogging(b =>
                    {
                        b.AddConsole();
                        b.SetMinimumLevel(LogLevel.Warning);
                    });

                    // PhotoStorage:Path is captured as a local variable early in Program.cs
                    // before ConfigureAppConfiguration overrides apply, so the repository and
                    // resizer must be replaced here where the seeded paths are used directly.
                    RemoveService<IPhotoRepository>(services);
                    services.AddSingleton<IPhotoRepository>(sp =>
                        new FileSystemPhotoRepository(
                            _seeded.BasePath,
                            _seeded.EventName,
                            sp.GetRequiredService<ILogger<FileSystemPhotoRepository>>()));

                    RemoveService<IImageResizer>(services);
                    services.AddSingleton<IImageResizer>(sp =>
                        new OpenCvImageResizer(
                            sp.GetRequiredService<IPhotoRepository>(),
                            Path.Combine(_seeded.BasePath, ".thumbnails"),
                            jpegQuality: 80,
                            sp.GetRequiredService<ILogger<OpenCvImageResizer>>()));

                    // Photos are pre-warmed by PhotoSeeder; disable background warmup
                    // so it doesn't compete with load-test requests at startup.
                    var warmup = services.FirstOrDefault(d =>
                        d.ServiceType == typeof(IHostedService) &&
                        d.ImplementationType == typeof(ThumbnailWarmupService));
                    if (warmup != null) services.Remove(warmup);
                });
            });

        // Trigger factory initialisation (creates TestServer, wires DI, loads photos)
        using var warmupClient = _factory.CreateClient();
        warmupClient.GetAsync("/api/config").GetAwaiter().GetResult();
        testContext.WriteLine("Server initialised");
    }

    [ClassCleanup]
    public static void ClassCleanup()
    {
        _factory?.Dispose();
        _seeded?.Cleanup();
    }

    [TestMethod]
    public void RunGuestLoadScenario()
    {
        var targetVUs   = int.Parse(Environment.GetEnvironmentVariable("LOADTEST_TARGET_USERS")  ?? "50");
        var holdMinutes = int.Parse(Environment.GetEnvironmentVariable("LOADTEST_DURATION_MIN")  ?? "3");

        var photos = _seeded.Photos;

        // Allocate virtual users proportionally to simulate the real visitor mix
        var galleryVUs  = Math.Max(1, targetVUs * 60 / 100);
        var detailVUs   = Math.Max(1, targetVUs * 30 / 100);
        var downloadVUs = Math.Max(1, targetVUs * 10 / 100);

        using var galleryClient  = CreateHttpClient();
        using var detailClient   = CreateHttpClient();
        using var downloadClient = CreateHttpClient();

        var galleryScenario  = BuildGalleryBrowseScenario(galleryClient,  photos, galleryVUs,  holdMinutes);
        var detailScenario   = BuildDetailViewScenario(detailClient,   photos, detailVUs,   holdMinutes);
        var downloadScenario = BuildDownloadScenario(downloadClient, photos, downloadVUs, holdMinutes);

        var reportFolder = Path.Combine(
            AppContext.BaseDirectory, "load-reports", DateTime.UtcNow.ToString("yyyyMMdd-HHmmss"));

        var ctx = NBomberRunner.RegisterScenarios([galleryScenario, detailScenario, downloadScenario]);
        ctx = NBomberRunner.WithTestSuite(ctx, "PhotoBoothGuestLoad");
        ctx = NBomberRunner.WithTestName(ctx, $"vus-{targetVUs}_photos-{photos.Count}");
        ctx = NBomberRunner.WithReportFolder(ctx, reportFolder);
        ctx = NBomberRunner.WithReportFormats(ctx, [ReportFormat.Html, ReportFormat.Txt]);

        var nodeStats = NBomberRunner.Run(ctx);

        PrintSummary(nodeStats, targetVUs, holdMinutes, photos.Count, reportFolder);

        var totalRequests = nodeStats.AllRequestCount;
        var totalFails    = nodeStats.AllFailCount;
        var errorRate     = totalRequests > 0 ? (double)totalFails / totalRequests : 0;

        Assert.IsTrue(errorRate < 0.01,
            $"Error rate {errorRate:P2} exceeded 1% " +
            $"({totalFails:N0} failures / {totalRequests:N0} requests). " +
            "The server is returning errors under this load, not just slowing down.");
    }

    // ── Scenario builders ─────────────────────────────────────────────────────

    // Models a guest opening the gallery and scrolling through a page of photos.
    // Mirrors PhotoGrid.tsx: fetches /api/photos?limit=30 then requests 30 thumbnails
    // at width=400 (up to 6 in parallel, matching browser connection limit).
    private static ScenarioProps BuildGalleryBrowseScenario(
        HttpClient client, IReadOnlyList<Photo> photos, int vus, int holdMinutes)
    {
        var scenario = Scenario.Create("gallery_browse", async context =>
        {
            try
            {
                // Initial config fetch (once per session; modelled as part of first iteration)
                await client.GetAsync("/api/config", context.ScenarioCancellationToken);

                // Gallery listing
                var pageResp = await client.GetAsync(
                    "/api/photos?limit=30", context.ScenarioCancellationToken);
                if (!pageResp.IsSuccessStatusCode)
                    return Response.Fail(statusCode: ((int)pageResp.StatusCode).ToString());

                var page = await pageResp.Content.ReadFromJsonAsync<PhotoPageDto>(
                    JsonOptions, context.ScenarioCancellationToken);
                if (page is null) return Response.Fail(statusCode: "parse_error");

                // Fetch thumbnails with browser-like concurrency (6 parallel)
                var pagePhotos = page.Photos.Take(30).ToList();
                using var sem = new SemaphoreSlim(6, 6);
                var thumbTasks = pagePhotos.Select(async p =>
                {
                    await sem.WaitAsync(context.ScenarioCancellationToken);
                    try
                    {
                        var r = await client.GetAsync(
                            $"/api/photos/{p.Id}/image?width=400",
                            context.ScenarioCancellationToken);
                        await r.Content.ReadAsByteArrayAsync(context.ScenarioCancellationToken);
                        return r.IsSuccessStatusCode;
                    }
                    finally { sem.Release(); }
                });
                var thumbResults = await Task.WhenAll(thumbTasks);
                if (thumbResults.Any(ok => !ok)) return Response.Fail(statusCode: "thumb_error");

                // Think time: user browses the gallery page
                await Task.Delay(
                    TimeSpan.FromSeconds(4 + context.Random.NextDouble() * 4),
                    context.ScenarioCancellationToken);

                // 50% chance: scroll to the second page
                if (page.NextCursor is not null && context.Random.NextDouble() < 0.5)
                {
                    var page2Resp = await client.GetAsync(
                        $"/api/photos?limit=30&cursor={Uri.EscapeDataString(page.NextCursor)}",
                        context.ScenarioCancellationToken);
                    if (page2Resp.IsSuccessStatusCode)
                    {
                        var page2 = await page2Resp.Content.ReadFromJsonAsync<PhotoPageDto>(
                            JsonOptions, context.ScenarioCancellationToken);
                        if (page2?.Photos is { Count: > 0 } p2Photos)
                        {
                            using var sem2 = new SemaphoreSlim(6, 6);
                            var page2Tasks = p2Photos.Take(30).Select(async p =>
                            {
                                await sem2.WaitAsync(context.ScenarioCancellationToken);
                                try
                                {
                                    var r = await client.GetAsync(
                                        $"/api/photos/{p.Id}/image?width=400",
                                        context.ScenarioCancellationToken);
                                    await r.Content.ReadAsByteArrayAsync(context.ScenarioCancellationToken);
                                }
                                finally { sem2.Release(); }
                            });
                            await Task.WhenAll(page2Tasks);
                        }
                    }
                }

                return Response.Ok();
            }
            catch (OperationCanceledException)
            {
                return Response.Ok(); // normal shutdown
            }
        });

        return Scenario.WithLoadSimulations(scenario, [
            Simulation.KeepConstant(Math.Min(5, vus), TimeSpan.FromSeconds(30)),
            Simulation.RampingConstant(vus, TimeSpan.FromMinutes(2)),
            Simulation.KeepConstant(vus, TimeSpan.FromMinutes(holdMinutes))
        ]);
    }

    // Models a guest viewing a specific photo — most expensive single request path because
    // PhotoDetailPage.tsx fetches GET /api/photos (the full unpaginated list) for prev/next nav.
    private static ScenarioProps BuildDetailViewScenario(
        HttpClient client, IReadOnlyList<Photo> photos, int vus, int holdMinutes)
    {
        var scenario = Scenario.Create("detail_view", async context =>
        {
            try
            {
                var photo = photos[context.Random.Next(photos.Count)];

                // Resolve code → PhotoDto
                var codeResp = await client.GetAsync(
                    $"/api/photos/{photo.Code}", context.ScenarioCancellationToken);
                if (!codeResp.IsSuccessStatusCode)
                    return Response.Fail(statusCode: ((int)codeResp.StatusCode).ToString());

                // Full list for prev/next nav — intentionally exercised as it's expensive at scale
                var listResp = await client.GetAsync("/api/photos", context.ScenarioCancellationToken);
                if (!listResp.IsSuccessStatusCode)
                    return Response.Fail(statusCode: ((int)listResp.StatusCode).ToString());
                await listResp.Content.ReadAsByteArrayAsync(context.ScenarioCancellationToken);

                // Display-size image
                var imgResp = await client.GetAsync(
                    $"/api/photos/{photo.Id}/image?width=1200", context.ScenarioCancellationToken);
                if (!imgResp.IsSuccessStatusCode)
                    return Response.Fail(statusCode: ((int)imgResp.StatusCode).ToString());
                await imgResp.Content.ReadAsByteArrayAsync(context.ScenarioCancellationToken);

                await Task.Delay(
                    TimeSpan.FromSeconds(6 + context.Random.NextDouble() * 6),
                    context.ScenarioCancellationToken);

                return Response.Ok();
            }
            catch (OperationCanceledException)
            {
                return Response.Ok();
            }
        });

        return Scenario.WithLoadSimulations(scenario, [
            Simulation.KeepConstant(Math.Min(5, vus), TimeSpan.FromSeconds(30)),
            Simulation.RampingConstant(vus, TimeSpan.FromMinutes(2)),
            Simulation.KeepConstant(vus, TimeSpan.FromMinutes(holdMinutes))
        ]);
    }

    // Models a guest downloading the full-resolution original.
    private static ScenarioProps BuildDownloadScenario(
        HttpClient client, IReadOnlyList<Photo> photos, int vus, int holdMinutes)
    {
        var scenario = Scenario.Create("download", async context =>
        {
            try
            {
                var photo = photos[context.Random.Next(photos.Count)];

                var resp = await client.GetAsync(
                    $"/api/photos/{photo.Id}/image", context.ScenarioCancellationToken);
                if (!resp.IsSuccessStatusCode)
                    return Response.Fail(statusCode: ((int)resp.StatusCode).ToString());

                var bytes = await resp.Content.ReadAsByteArrayAsync(context.ScenarioCancellationToken);

                await Task.Delay(
                    TimeSpan.FromSeconds(20 + context.Random.NextDouble() * 20),
                    context.ScenarioCancellationToken);

                return Response.Ok(sizeBytes: bytes.LongLength);
            }
            catch (OperationCanceledException)
            {
                return Response.Ok();
            }
        });

        return Scenario.WithLoadSimulations(scenario, [
            Simulation.KeepConstant(Math.Min(2, vus), TimeSpan.FromSeconds(30)),
            Simulation.RampingConstant(vus, TimeSpan.FromMinutes(2)),
            Simulation.KeepConstant(vus, TimeSpan.FromMinutes(holdMinutes))
        ]);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static HttpClient CreateHttpClient()
    {
        var handler = new LatencyHandler(TimeSpan.FromMilliseconds(100))
        {
            InnerHandler = _factory.Server.CreateHandler()
        };
        return new HttpClient(handler) { BaseAddress = _factory.Server.BaseAddress };
    }

    private static void RemoveService<T>(IServiceCollection services)
    {
        var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(T));
        if (descriptor != null) services.Remove(descriptor);
    }

    private static void PrintSummary(
        NodeStats stats, int targetVUs, int holdMinutes, int photoCount, string reportFolder)
    {
        Console.WriteLine();
        Console.WriteLine("════════════════════════════════════════════════════════");
        Console.WriteLine(" PHOTO BOOTH GUEST LOAD TEST — SUMMARY");
        Console.WriteLine("════════════════════════════════════════════════════════");
        Console.WriteLine($" Config : {targetVUs} VUs | {photoCount} photos | {holdMinutes} min hold");
        Console.WriteLine($" Total  : {stats.AllRequestCount:N0} requests | {stats.AllFailCount:N0} failures");
        Console.WriteLine();

        foreach (var s in stats.ScenarioStats)
        {
            var ok   = s.Ok;
            var fail = s.Fail;
            Console.WriteLine($" [{s.ScenarioName}]");
            Console.WriteLine($"   OK    {ok.Request.Count,8:N0} req  |  {ok.Request.RPS,7:F1} RPS");
            Console.WriteLine($"   Fail  {fail.Request.Count,8:N0} req");
            Console.WriteLine($"   Latency  p50={ok.Latency.Percent50:F0}ms  " +
                              $"p95={ok.Latency.Percent95:F0}ms  " +
                              $"p99={ok.Latency.Percent99:F0}ms  " +
                              $"(includes 100ms simulated RTT)");
            Console.WriteLine();
        }

        Console.WriteLine(" ── Analytic bandwidth ceiling ──────────────────────");
        Console.WriteLine("   Client 10 Mbps:");
        Console.WriteLine($"     Full-res ~2MB photo   → {2.0 * 8 / 10.0:F1}s minimum download");
        Console.WriteLine($"     Gallery page (30×50KB)→ {30 * 50.0 * 8 / (10.0 * 1024):F1}s minimum thumbnail load");
        Console.WriteLine("   Server 200 Mbps:");
        Console.WriteLine($"     Max concurrent full-res downloads → {200 / (2.0 * 8):F0}");
        Console.WriteLine();
        Console.WriteLine(" ── How to find the limit ────────────────────────────");
        Console.WriteLine("   Increase LOADTEST_TARGET_USERS until p95 of gallery_browse");
        Console.WriteLine("   (minus 100ms baseline) exceeds ~2s, or error rate > 1%.");
        Console.WriteLine("   That VU count is the answer to 'how many concurrent guests'.");
        Console.WriteLine();
        Console.WriteLine($" Report → {reportFolder}");
        Console.WriteLine("════════════════════════════════════════════════════════");
    }
}
