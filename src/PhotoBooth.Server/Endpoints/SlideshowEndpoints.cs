using PhotoBooth.Application.Services;

namespace PhotoBooth.Server.Endpoints;

public static class SlideshowEndpoints
{
    public static void MapSlideshowEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/slideshow");

        group.MapGet("/next", GetNextPhoto)
            .WithName("GetNextSlideshowPhoto");
    }

    private static async Task<IResult> GetNextPhoto(
        ISlideshowService slideshowService,
        CancellationToken cancellationToken)
    {
        var photo = await slideshowService.GetNextAsync(cancellationToken);
        return photo is null ? Results.NotFound() : Results.Ok(photo);
    }
}
