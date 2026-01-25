using FlashCap;
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

        group.MapGet("/devices", ListDevices)
            .WithName("ListCameraDevices");
    }

    private static async Task<IResult> GetCameraInfo(
        ICameraProvider cameraProvider,
        CancellationToken cancellationToken)
    {
        var isAvailable = await cameraProvider.IsAvailableAsync(cancellationToken);
        var latencyMs = (int)cameraProvider.CaptureLatency.TotalMilliseconds;

        return Results.Ok(new CameraInfoDto(isAvailable, latencyMs));
    }

    private static IResult ListDevices()
    {
        try
        {
            var devices = new CaptureDevices();
            var descriptors = devices.EnumerateDescriptors().ToList();

            var result = descriptors.Select((d, i) => new
            {
                Index = i,
                Name = d.Name,
                Characteristics = d.Characteristics.Select(c => new
                {
                    c.Width,
                    c.Height,
                    c.FramesPerSecond,
                    PixelFormat = c.PixelFormat.ToString()
                }).ToList()
            }).ToList();

            return Results.Ok(result);
        }
        catch (Exception ex)
        {
            return Results.Problem(detail: ex.Message, title: "Failed to enumerate cameras");
        }
    }
}
