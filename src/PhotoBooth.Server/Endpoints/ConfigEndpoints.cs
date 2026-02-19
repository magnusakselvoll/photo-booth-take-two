using PhotoBooth.Application.DTOs;

namespace PhotoBooth.Server.Endpoints;

public static class ConfigEndpoints
{
    public static void MapConfigEndpoints(this IEndpointRouteBuilder app, IConfiguration configuration)
    {
        app.MapGet("/api/config", () => GetConfig(configuration))
            .WithName("GetConfig");
    }

    private static IResult GetConfig(IConfiguration configuration)
    {
        var qrCodeBaseUrl = configuration.GetValue<string>("QrCode:BaseUrl");
        var swirlEffect = configuration.GetValue<bool>("Slideshow:SwirlEffect", true);
        var slideshowIntervalMs = configuration.GetValue<int?>("Slideshow:IntervalMs") ?? 30000;

        var gamepadSection = configuration.GetSection("Input:Gamepad");
        var gamepadEnabled = gamepadSection.GetValue<bool>("Enabled", false);
        var gamepadDebugMode = gamepadSection.GetValue<bool>("DebugMode", false);
        var buttonsSection = gamepadSection.GetSection("Buttons");
        var buttons = new GamepadButtonsDto(
            Next: buttonsSection.GetSection("Next").Get<int[]>() ?? [5, 15],
            Previous: buttonsSection.GetSection("Previous").Get<int[]>() ?? [4, 14],
            SkipForward: buttonsSection.GetSection("SkipForward").Get<int[]>() ?? [3, 13],
            SkipBackward: buttonsSection.GetSection("SkipBackward").Get<int[]>() ?? [2, 12],
            TriggerCapture: buttonsSection.GetSection("TriggerCapture").Get<int[]>() ?? [0],
            TriggerCapture1s: buttonsSection.GetSection("TriggerCapture1s").Get<int[]>() ?? [],
            TriggerCapture3s: buttonsSection.GetSection("TriggerCapture3s").Get<int[]>() ?? [],
            TriggerCapture5s: buttonsSection.GetSection("TriggerCapture5s").Get<int[]>() ?? [],
            ToggleMode: buttonsSection.GetSection("ToggleMode").Get<int[]>() ?? [8]);
        var dpadAxesSection = gamepadSection.GetSection("DpadAxes");
        var dpadAxes = new GamepadDpadAxesDto(
            HorizontalAxisIndex: dpadAxesSection.GetValue<int>("HorizontalAxisIndex", 6),
            VerticalAxisIndex: dpadAxesSection.GetValue<int>("VerticalAxisIndex", 7),
            Threshold: dpadAxesSection.GetValue<double>("Threshold", 0.5));
        var gamepad = new GamepadConfigDto(gamepadEnabled, gamepadDebugMode, buttons, dpadAxes);

        var config = new ClientConfigDto(qrCodeBaseUrl, swirlEffect, slideshowIntervalMs, gamepad);
        return Results.Ok(config);
    }
}
