namespace GafiFish.Core.Casting;

/// <summary>
/// Predicts when the cast indicator reaches the calibrated target zone.
/// This class does not send mouse input.
/// </summary>
public sealed class PerfectCastController
{
    private readonly PerfectCastSettings _settings;

    public PerfectCastController(PerfectCastSettings? settings = null)
    {
        _settings = settings ?? new PerfectCastSettings();
        _settings.Validate();
    }

    public PerfectCastDecision Evaluate(CastMeterObservation observation, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(observation);

        if (!observation.IsVisible)
        {
            return Cancel("Cast meter is not visible.");
        }

        if (!double.IsFinite(observation.Confidence)
            || observation.Confidence < _settings.MinimumConfidence)
        {
            return Cancel("Cast meter detection confidence is too low.");
        }

        var age = now - observation.CapturedAt;
        if (age < TimeSpan.Zero || age > _settings.MaximumFrameAge)
        {
            return Cancel("Cast meter frame is stale or has an invalid timestamp.");
        }

        if (!IsFinite01(observation.IndicatorPosition)
            || !IsFinite01(observation.TargetStart)
            || !IsFinite01(observation.TargetEnd)
            || observation.TargetStart >= observation.TargetEnd
            || !double.IsFinite(observation.IndicatorVelocityPerSecond))
        {
            return Cancel("Cast meter observation is outside the calibrated range.");
        }

        var position = observation.IndicatorPosition;
        var velocity = observation.IndicatorVelocityPerSecond;
        var predictedPosition = position + velocity * _settings.EstimatedInputLatency.TotalSeconds;

        if (!double.IsFinite(predictedPosition))
        {
            return Cancel("Cast meter prediction is not finite.");
        }

        var insideTarget = position >= observation.TargetStart
            && position <= observation.TargetEnd;

        if (insideTarget)
        {
            return Release(predictedPosition, "The indicator is currently inside the target zone.");
        }

        if (velocity >= 0)
        {
            if (position > observation.TargetEnd)
            {
                return Cancel("The indicator has already passed the target zone.");
            }

            if (predictedPosition >= observation.TargetStart)
            {
                return Release(
                    predictedPosition,
                    predictedPosition > observation.TargetEnd
                        ? "Release immediately; input latency may carry the indicator across the target."
                        : "The indicator is predicted to enter the target zone during input latency.");
            }
        }
        else
        {
            if (position < observation.TargetStart)
            {
                return Cancel("The indicator has already passed the target zone in the reverse direction.");
            }

            if (predictedPosition <= observation.TargetEnd)
            {
                return Release(
                    predictedPosition,
                    predictedPosition < observation.TargetStart
                        ? "Release immediately; input latency may carry the indicator across the target."
                        : "The indicator is predicted to enter the target zone during input latency.");
            }
        }

        return new PerfectCastDecision(
            PerfectCastDecisionKind.Wait,
            "The indicator has not reached the release window.",
            predictedPosition);
    }

    private static bool IsFinite01(double value) =>
        double.IsFinite(value) && value is >= 0 and <= 1;

    private static PerfectCastDecision Release(double predictedPosition, string reason) =>
        new(PerfectCastDecisionKind.Release, reason, predictedPosition);

    private static PerfectCastDecision Cancel(string reason) =>
        new(PerfectCastDecisionKind.Cancel, reason, double.NaN);
}
