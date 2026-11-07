namespace GafiFish.Windows.Capture;

/// <summary>
/// Verifies that the exact Roblox process remains the foreground input target.
/// </summary>
public interface IRobloxWindowCapture
{
    ScreenCaptureFrame CaptureForegroundClient();
    bool IsRobloxForeground();
    bool IsRobloxForeground(int expectedProcessId);
    int? GetForegroundRobloxProcessId();
}
