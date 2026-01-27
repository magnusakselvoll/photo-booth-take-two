using PhotoBooth.Application.Services;
using PhotoBooth.Domain.Exceptions;

namespace PhotoBooth.Server.Endpoints;

public static class PhotoEndpoints
{
    public static void MapPhotoEndpoints(this IEndpointRouteBuilder app, IEndpointFilter? triggerFilter = null)
    {
        var group = app.MapGroup("/api/photos");

        var triggerEndpoint = group.MapPost("/trigger", TriggerCapture)
            .WithName("TriggerCapture");

        if (triggerFilter is not null)
        {
            triggerEndpoint.AddEndpointFilter(triggerFilter);
        }

        group.MapPost("/capture", CapturePhoto)
            .WithName("CapturePhoto");

        group.MapGet("/{code}", GetPhotoByCode)
            .WithName("GetPhotoByCode");

        group.MapGet("/{id:guid}/image", GetPhotoImage)
            .WithName("GetPhotoImage");
    }

    private static async Task<IResult> TriggerCapture(
        ICaptureWorkflowService workflowService,
        CancellationToken cancellationToken)
    {
        await workflowService.TriggerCaptureAsync("web-ui", cancellationToken);

        return Results.Accepted(value: new
        {
            message = "Capture workflow started",
            countdownDurationMs = workflowService.CountdownDurationMs
        });
    }

    private static async Task<IResult> CapturePhoto(
        IPhotoCaptureService captureService,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await captureService.CaptureAsync(cancellationToken);
            return Results.Ok(result);
        }
        catch (CameraNotAvailableException ex)
        {
            return Results.Problem(
                detail: ex.Message,
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Camera Not Available");
        }
    }

    private static async Task<IResult> GetPhotoByCode(
        string code,
        IPhotoCaptureService captureService,
        CancellationToken cancellationToken)
    {
        var photo = await captureService.GetByCodeAsync(code, cancellationToken);
        return photo is null ? Results.NotFound() : Results.Ok(photo);
    }

    private static async Task<IResult> GetPhotoImage(
        Guid id,
        IPhotoCaptureService captureService,
        CancellationToken cancellationToken)
    {
        var imageData = await captureService.GetImageDataAsync(id, cancellationToken);
        return imageData is null
            ? Results.NotFound()
            : Results.File(imageData, "image/jpeg");
    }
}
