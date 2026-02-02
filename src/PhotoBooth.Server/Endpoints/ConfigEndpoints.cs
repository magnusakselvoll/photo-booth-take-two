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
        var config = new ClientConfigDto(qrCodeBaseUrl, swirlEffect);
        return Results.Ok(config);
    }
}
