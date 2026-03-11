namespace PhotoBooth.Domain.Entities;

public record Photo
{
    public Guid Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public string FilePath { get; init; } = string.Empty;
    public DateTime CapturedAt { get; init; }
}
