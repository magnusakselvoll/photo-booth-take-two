namespace PhotoBooth.Application.DTOs;

public record ClientConfigDto(string? QrCodeBaseUrl, bool SwirlEffect, int SlideshowIntervalMs);
