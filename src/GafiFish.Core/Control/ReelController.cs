namespace GafiFish.Core.Control;

/// <summary>
/// Computes the desired hold/release state from visual measurements.
/// This class does not send input; the automation coordinator must apply its
/// output only when the Roblox window and current frame are validated.
/// </summary>
public sealed class ReelController
{
    private readonly ReelControlSettings _settings;

    public ReelController(ReelControlSettings? settings = null)
    {
        _settings = settings ?? new ReelControlSettings();
        _settings.Validate();
    }

    public ReelControlDecision Evaluate(
        double fishPositionX,
        double controlPositionX,
        double fishVelocityPixelsPerSecond,
        double confidence,
        bool isFresh)
    {
        if (!isFresh || !double.IsFinite(confidence)
            || confidence < _settings.MinimumConfidence)
        {
            return Neutral(0, 0, "Visual tracking is stale or below the confidence threshold.");
        }

        if (!double.IsFinite(fishPositionX)
            || !double.IsFinite(controlPositionX)
            || !double.IsFinite(fishVelocityPixelsPerSecond)
            || confidence is < 0 or > 1)
        {
            return Neutral(0, 0, "Visual measurements are invalid.");
        }

        var predictedFishX =
            fishPositionX + fishVelocityPixelsPerSecond * _settings.LookAheadSeconds;
        var error = predictedFishX - controlPositionX;
        var signal = _settings.ProportionalGain * error
            + _settings.DerivativeGain * fishVelocityPixelsPerSecond;

        if (Math.Abs(signal) <= _settings.Deadband)
        {
            return new ReelControlDecision(
                ReelCommand.Maintain,
                error,
                signal,
                "Target is inside the controller deadband; keep the previous input state.");
        }

        var positive = signal > 0;
        var hold = positive == _settings.PositiveErrorMeansHold;
        return new ReelControlDecision(
            hold ? ReelCommand.Hold : ReelCommand.Release,
            error,
            signal,
            hold
                ? "Predictive controller requests a hold."
                : "Predictive controller requests a release.");
    }

    private static ReelControlDecision Neutral(double error, double signal, string reason) =>
        new(ReelCommand.Neutral, error, signal, reason);
}
