namespace PhotoBooth.Application.DTOs;

public record SlideshowPhotoDto(Guid Id, string Code, DateTime CapturedAt, string ImageUrl);
