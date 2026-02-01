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
        var config = new ClientConfigDto(qrCodeBaseUrl);
        return Results.Ok(config);
    }
}
