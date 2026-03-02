namespace PhotoBooth.Application.DTOs;

public record PhotoDto(Guid Id, string Code, DateTime CapturedAt);

public record PhotoPageDto(IReadOnlyList<PhotoDto> Photos, string? NextCursor);
