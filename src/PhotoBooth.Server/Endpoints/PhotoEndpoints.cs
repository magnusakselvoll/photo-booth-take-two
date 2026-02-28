using Microsoft.Net.Http.Headers;
using PhotoBooth.Application.Services;
using PhotoBooth.Domain.Exceptions;

namespace PhotoBooth.Server.Endpoints;

public static class PhotoEndpoints
{
    public static void MapPhotoEndpoints(this IEndpointRouteBuilder app, IEndpointFilter? triggerFilter = null)
    {
        var group = app.MapGroup("/api/photos");

        var triggerEndpoint = group.MapPost("/trigger", TriggerCapture)
            .WithName("TriggerCapture")
            .RequireRateLimiting("capture");

        if (triggerFilter is not null)
        {
            triggerEndpoint.AddEndpointFilter(triggerFilter);
        }

        group.MapPost("/capture", CapturePhoto)
            .WithName("CapturePhoto")
            .RequireRateLimiting("capture");

        group.MapGet("/", GetAllPhotos)
            .WithName("GetAllPhotos");

        group.MapGet("/{code}", GetPhotoByCode)
            .WithName("GetPhotoByCode");

        group.MapGet("/{id:guid}/image", GetPhotoImage)
            .WithName("GetPhotoImage");
    }

    private static async Task<IResult> TriggerCapture(
        ICaptureWorkflowService workflowService,
        int? durationMs,
        CancellationToken cancellationToken)
    {
        var effectiveDuration = durationMs ?? workflowService.CountdownDurationMs;
        await workflowService.TriggerCaptureAsync("web-ui", durationMs, cancellationToken);

        return Results.Accepted(value: new
        {
            message = "Capture workflow started",
            countdownDurationMs = effectiveDuration
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
        int? width,
        IPhotoCaptureService captureService,
        IImageResizer imageResizer,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        byte[]? imageData;

        if (width.HasValue)
        {
            imageData = await imageResizer.GetResizedImageAsync(id, width.Value, cancellationToken);
        }
        else
        {
            imageData = await captureService.GetImageDataAsync(id, cancellationToken);
        }

        if (imageData is null)
        {
            return Results.NotFound();
        }

        httpContext.Response.Headers[HeaderNames.CacheControl] = "public, max-age=31536000, immutable";
        return Results.File(imageData, "image/jpeg");
    }

    private static async Task<IResult> GetAllPhotos(
        IPhotoCaptureService captureService,
        CancellationToken cancellationToken)
    {
        var photos = await captureService.GetAllAsync(cancellationToken);
        return Results.Ok(photos);
    }
}
