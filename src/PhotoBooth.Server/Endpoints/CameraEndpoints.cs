using PhotoBooth.Application.DTOs;
using PhotoBooth.Domain.Interfaces;

namespace PhotoBooth.Server.Endpoints;

public static class CameraEndpoints
{
    public static void MapCameraEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/camera");

        group.MapGet("/info", GetCameraInfo)
            .WithName("GetCameraInfo");
    }

    private static async Task<IResult> GetCameraInfo(
        ICameraProvider cameraProvider,
        CancellationToken cancellationToken)
    {
        var isAvailable = await cameraProvider.IsAvailableAsync(cancellationToken);
        var latencyMs = (int)cameraProvider.CaptureLatency.TotalMilliseconds;

        return Results.Ok(new CameraInfoDto(isAvailable, latencyMs));
    }
}
