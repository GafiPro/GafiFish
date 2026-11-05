using GafiFish.Vision.Detection;

namespace GafiFish.Vision.Tracking;

public sealed record FishMotionEstimate(
    double PositionX,
    double VelocityPixelsPerSecond,
    double PredictedPositionX,
    double Confidence,
    DateTimeOffset ObservedAt,
    bool IsFresh);

/// <summary>
/// Smooths horizontal fish motion. Predictions are deliberately short and
/// expire quickly when detections stop, so stale frames cannot drive control.
/// </summary>
public sealed class FishMotionTracker
{
    private readonly double _smoothing;
    private readonly TimeSpan _maximumPredictionAge;
    private readonly TimeSpan _lookAhead;
    private double? _lastPosition;
    private double _velocity;
    private DateTimeOffset _lastObservedAt;

    public FishMotionTracker(
        double smoothing = 0.45,
        TimeSpan? maximumPredictionAge = null,
        TimeSpan? lookAhead = null)
    {
        if (!double.IsFinite(smoothing) || smoothing is <= 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(smoothing));
        }

        _smoothing = smoothing;
        _maximumPredictionAge = maximumPredictionAge ?? TimeSpan.FromMilliseconds(180);
        _lookAhead = lookAhead ?? TimeSpan.FromMilliseconds(90);

        if (_maximumPredictionAge <= TimeSpan.Zero || _lookAhead < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumPredictionAge));
        }
    }

    public FishMotionEstimate? Update(DetectedMarker? marker, DateTimeOffset capturedAt)
    {
        if (marker is null)
        {
            return GetEstimate(capturedAt);
        }

        if (!double.IsFinite(marker.CenterX) || marker.Confidence is < 0 or > 1)
        {
            return GetEstimate(capturedAt);
        }

        if (_lastPosition.HasValue)
        {
            var elapsed = (capturedAt - _lastObservedAt).TotalSeconds;
            if (elapsed > 0 && elapsed <= 1.0)
            {
                var measuredVelocity = (marker.CenterX - _lastPosition.Value) / elapsed;
                _velocity = _velocity == 0
                    ? measuredVelocity
                    : _smoothing * measuredVelocity + (1 - _smoothing) * _velocity;
            }
            else
            {
                _velocity = 0;
            }
        }
        else
        {
            _velocity = 0;
        }

        _lastPosition = marker.CenterX;
        _lastObservedAt = capturedAt;

        return new FishMotionEstimate(
            marker.CenterX,
            _velocity,
            marker.CenterX + _velocity * _lookAhead.TotalSeconds,
            marker.Confidence,
            capturedAt,
            true);
    }

    public FishMotionEstimate? GetEstimate(DateTimeOffset now)
    {
        if (!_lastPosition.HasValue)
        {
            return null;
        }

        var age = now - _lastObservedAt;
        if (age < TimeSpan.Zero || age > _maximumPredictionAge)
        {
            Reset();
            return null;
        }

        var confidence = Math.Clamp(
            (1 - age.TotalMilliseconds / _maximumPredictionAge.TotalMilliseconds) * 0.75,
            0, 0.75);

        return new FishMotionEstimate(
            _lastPosition.Value,
            _velocity,
            _lastPosition.Value + _velocity * _lookAhead.TotalSeconds,
            confidence,
            _lastObservedAt,
            false);
    }

    public void Reset()
    {
        _lastPosition = null;
        _velocity = 0;
        _lastObservedAt = default;
    }
}
