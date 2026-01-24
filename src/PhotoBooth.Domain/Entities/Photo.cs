namespace PhotoBooth.Domain.Entities;

public class Photo
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public DateTime CapturedAt { get; set; }
}
