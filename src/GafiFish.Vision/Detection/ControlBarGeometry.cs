using OpenCvSharp;

namespace GafiFish.Vision.Detection;

/// <summary>
/// Horizontal control-bar slice at the vertical position of the fish marker.
/// </summary>
public readonly record struct HorizontalBarSpan(double Left, double Right)
{
    public double CenterX => (Left + Right) / 2d;
    public double Width => Math.Max(0d, Right - Left);
}

/// <summary>
/// Computes the horizontal part of the control bar that is actually present
/// at the fish's height. Rotated bars must not use their enclosing AABB as
/// though every x-coordinate in that box were covered.
/// </summary>
public static class ControlBarGeometry
{
    public static bool TryGetHorizontalSpanAtY(
        DetectedMarker marker,
        double y,
        out HorizontalBarSpan span)
    {
        ArgumentNullException.ThrowIfNull(marker);
        span = default;

        if (!double.IsFinite(y) || marker.Bounds.Width <= 0)
            return false;

        // Preserve the established horizontal-bar behaviour for markers that
        // do not need an oriented rectangle.
        if (marker.OrientedBounds is not { } oriented)
        {
            span = new HorizontalBarSpan(marker.Bounds.Left, marker.Bounds.Right);
            return span.Width > 0;
        }

        var width = oriented.Size.Width;
        var height = oriented.Size.Height;
        if (!float.IsFinite(width) || !float.IsFinite(height) || width <= 0 || height <= 0)
            return false;

        // OpenCV may normalize a RotatedRect by swapping Width/Height and
        // adjusting Angle. Use its own vertices so the representation remains
        // correct regardless of that normalization convention.
        var corners = oriented.Points();

        var intersections = new List<double>(4);
        for (var index = 0; index < corners.Length; index++)
        {
            var first = corners[index];
            var second = corners[(index + 1) % corners.Length];
            AddHorizontalIntersection(intersections, first, second, y);
        }

        if (intersections.Count < 2)
            return false;

        var left = intersections.Min();
        var right = intersections.Max();
        if (!double.IsFinite(left) || !double.IsFinite(right) || right - left < 0.5)
            return false;

        span = new HorizontalBarSpan(left, right);
        return true;
    }

    private static void AddHorizontalIntersection(
        ICollection<double> intersections,
        Point2f first,
        Point2f second,
        double y)
    {
        const double epsilon = 1e-5;
        var deltaY = second.Y - first.Y;

        if (Math.Abs(deltaY) <= epsilon)
        {
            if (Math.Abs(y - first.Y) <= epsilon)
            {
                intersections.Add(first.X);
                intersections.Add(second.X);
            }
            return;
        }

        var amount = (y - first.Y) / deltaY;
        if (amount < -epsilon || amount > 1d + epsilon)
            return;

        amount = Math.Clamp(amount, 0d, 1d);
        intersections.Add(first.X + amount * (second.X - first.X));
    }
}
