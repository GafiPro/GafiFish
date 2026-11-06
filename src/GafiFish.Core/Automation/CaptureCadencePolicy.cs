namespace GafiFish.Core.Automation;

public enum CapturePerformanceMode
{
    Economy,
    Balanced,
    Responsive
}

/// <summary>
/// Adaptive screen-capture pacing presets. More frequent visual analysis can
/// increase CPU usage.
/// </summary>
public static class CaptureCadencePolicy
{
    public static int GetTargetIntervalMilliseconds(CapturePerformanceMode mode) =>
        mode switch
        {
            CapturePerformanceMode.Economy => 260,
            CapturePerformanceMode.Responsive => 55,
            _ => 120
        };

    public static int CalculateDelayMilliseconds(
        CapturePerformanceMode mode,
        double processingMilliseconds)
    {
        if (!double.IsFinite(processingMilliseconds) || processingMilliseconds < 0)
            throw new ArgumentOutOfRangeException(nameof(processingMilliseconds));

        var target = GetTargetIntervalMilliseconds(mode);
        var minimum = mode == CapturePerformanceMode.Responsive ? 8 : 15;
        return Math.Clamp((int)Math.Ceiling(target - processingMilliseconds), minimum, target);
    }
}
