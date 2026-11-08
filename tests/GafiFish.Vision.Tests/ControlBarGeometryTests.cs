using GafiFish.Vision.Detection;
using OpenCvSharp;
using Xunit;

namespace GafiFish.Vision.Tests;

public sealed class ControlBarGeometryTests
{
    [Fact]
    public void HorizontalSpanAtCentreUsesOrientedRectangleInsteadOfItsBoundingBox()
    {
        var marker = Marker(new RotatedRect(
            new Point2f(100, 100),
            new Size2f(80, 10),
            45));

        var found = ControlBarGeometry.TryGetHorizontalSpanAtY(marker, 100, out var span);

        Assert.True(found);
        Assert.InRange(span.CenterX, 99.5, 100.5);
        Assert.InRange(span.Width, 62, 65);
        Assert.True(span.Width < marker.Bounds.Width);
    }

    [Fact]
    public void HorizontalSpanReturnsFalseWhenFishHeightMissesRotatedBar()
    {
        var marker = Marker(new RotatedRect(
            new Point2f(100, 100),
            new Size2f(80, 10),
            45));

        var found = ControlBarGeometry.TryGetHorizontalSpanAtY(marker, 160, out _);

        Assert.False(found);
    }

    [Fact]
    public void AxisAlignedMarkerKeepsExistingBoundsBehaviour()
    {
        var marker = new DetectedMarker(
            40, 30, new Rect(20, 27, 40, 6), 200, 0.95);

        var found = ControlBarGeometry.TryGetHorizontalSpanAtY(marker, -100, out var span);

        Assert.True(found);
        Assert.Equal(20, span.Left);
        Assert.Equal(60, span.Right);
    }

    private static DetectedMarker Marker(RotatedRect bounds)
    {
        var corners = bounds.Points();
        var left = (int)Math.Floor(corners.Min(point => point.X));
        var right = (int)Math.Ceiling(corners.Max(point => point.X));
        var top = (int)Math.Floor(corners.Min(point => point.Y));
        var bottom = (int)Math.Ceiling(corners.Max(point => point.Y));
        var rect = new Rect(left, top, right - left, bottom - top);
        return new DetectedMarker(bounds.Center.X, bounds.Center.Y, rect, rect.Width * rect.Height, 0.95)
        {
            OrientedBounds = bounds
        };
    }
}
