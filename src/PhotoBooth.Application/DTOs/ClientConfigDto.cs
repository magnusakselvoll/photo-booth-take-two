namespace PhotoBooth.Application.DTOs;

public record GamepadButtonsDto(
    int[] Next,
    int[] Previous,
    int[] SkipForward,
    int[] SkipBackward,
    int[] TriggerCapture,
    int[] TriggerCapture1s,
    int[] TriggerCapture3s,
    int[] TriggerCapture5s,
    int[] ToggleMode);

public record GamepadDpadAxesDto(int HorizontalAxisIndex, int VerticalAxisIndex, double Threshold);

public record GamepadConfigDto(bool Enabled, bool DebugMode, GamepadButtonsDto Buttons, GamepadDpadAxesDto DpadAxes);

public record ClientConfigDto(
    string? QrCodeBaseUrl,
    bool SwirlEffect,
    int SlideshowIntervalMs,
    GamepadConfigDto Gamepad);
