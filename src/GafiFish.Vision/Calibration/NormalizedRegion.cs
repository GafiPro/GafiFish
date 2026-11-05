using OpenCvSharp;

namespace GafiFish.Vision.Calibration;

public readonly record struct NormalizedRegion(double X, double Y, double Width, double Height)
{
    public static NormalizedRegion FullFrame => new(0, 0, 1, 1);

    public void Validate()
    {
        if (!double.IsFinite(X) || !double.IsFinite(Y)
            || !double.IsFinite(Width) || !double.IsFinite(Height)
            || X < 0 || Y < 0 || Width <= 0 || Height <= 0
            || X + Width > 1 || Y + Height > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(NormalizedRegion), "Region must fit inside the frame.");
        }
    }

    public Rect ToPixelRect(int frameWidth, int frameHeight)
    {
        Validate();
        if (frameWidth <= 0 || frameHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(frameWidth));
        }

        var left = Math.Clamp((int)Math.Floor(X * frameWidth), 0, frameWidth - 1);
        var top = Math.Clamp((int)Math.Floor(Y * frameHeight), 0, frameHeight - 1);
        var right = Math.Clamp((int)Math.Ceiling((X + Width) * frameWidth), left + 1, frameWidth);
        var bottom = Math.Clamp((int)Math.Ceiling((Y + Height) * frameHeight), top + 1, frameHeight);

        return new Rect(left, top, right - left, bottom - top);
    }
}
