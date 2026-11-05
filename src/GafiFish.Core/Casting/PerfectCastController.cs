namespace GafiFish.Core.Casting;

/// <summary>
/// Pure decision logic for Perfect Cast. It does not send mouse input.
/// The vision layer supplies calibrated meter observations and the Windows
/// input layer is responsible for acting on a Release decision.
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

        if (observation.Confidence < _settings.MinimumConfidence)
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
            || !double.IsFinite(observation.IndicatorSpeedPerSecond)
            || observation.IndicatorSpeedPerSecond < 0)
        {
            return Cancel("Cast meter observation is outside the calibrated range.");
        }

        var predictedPosition = observation.IndicatorPosition
            + observation.IndicatorSpeedPerSecond * _settings.EstimatedInputLatency.TotalSeconds;

        if (observation.IndicatorPosition > observation.TargetEnd)
        {
            return new PerfectCastDecision(
                PerfectCastDecisionKind.Cancel,
                "The target zone has already been passed.",
                predictedPosition);
        }

        if (predictedPosition >= observation.TargetStart)
        {
            return new PerfectCastDecision(
                PerfectCastDecisionKind.Release,
                "The indicator is predicted to enter the target zone during input latency.",
                predictedPosition);
        }

        return new PerfectCastDecision(
            PerfectCastDecisionKind.Wait,
            "The indicator has not reached the release window.",
            predictedPosition);
    }

    private static bool IsFinite01(double value) =>
        double.IsFinite(value) && value is >= 0 and <= 1;

    private static PerfectCastDecision Cancel(string reason) =>
        new(PerfectCastDecisionKind.Cancel, reason, double.NaN);
}
