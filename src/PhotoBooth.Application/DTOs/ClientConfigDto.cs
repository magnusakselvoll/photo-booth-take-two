namespace PhotoBooth.Application.DTOs;

public record GamepadButtonsDto(
    int[] Next,
    int[] Previous,
    int[] SkipForward,
    int[] SkipBackward,
    int[] TriggerCapture,
    int[] ToggleMode);

public record GamepadConfigDto(bool Enabled, bool DebugMode, GamepadButtonsDto Buttons);

public record ClientConfigDto(
    string? QrCodeBaseUrl,
    bool SwirlEffect,
    int SlideshowIntervalMs,
    GamepadConfigDto Gamepad);
