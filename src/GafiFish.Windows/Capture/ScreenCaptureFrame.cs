namespace GafiFish.Windows.Capture;

/// <summary>
/// A top-down 32-bit BGRA image captured from the visible client area of the
/// foreground Roblox window. Pixel data is owned by this frame.
/// </summary>
public sealed record ScreenCaptureFrame(
    int Width,
    int Height,
    int Stride,
    byte[] BgraPixels,
    DateTimeOffset CapturedAt,
    string WindowTitle,
    int ProcessId);
