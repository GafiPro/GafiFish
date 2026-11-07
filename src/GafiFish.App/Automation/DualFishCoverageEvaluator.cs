using GafiFish.Vision.Detection;

namespace GafiFish.App.Automation;

/// <summary>
/// Counts how many calibrated fish target centres are horizontally covered by the
/// detected control bar. This is telemetry only; it never sends input.
/// </summary>
public static class DualFishCoverageEvaluator
{
    public static int CountCoveredFish(
        DetectedMarker? firstFish,
        DetectedMarker? secondFish,
        DetectedMarker? controlBar)
    {
        if (controlBar is null)
            return 0;

        var left = controlBar.Bounds.Left;
        var right = controlBar.Bounds.Right;
        var covered = 0;

        if (IsInside(firstFish, left, right))
            covered++;
        if (IsInside(secondFish, left, right))
            covered++;

        return covered;
    }

    private static bool IsInside(DetectedMarker? fish, int left, int right) =>
        fish is not null && fish.CenterX >= left && fish.CenterX <= right;
}
