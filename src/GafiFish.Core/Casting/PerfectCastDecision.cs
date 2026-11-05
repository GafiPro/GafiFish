namespace GafiFish.Core.Casting;

public enum PerfectCastDecisionKind
{
    Wait,
    Release,
    Cancel
}

public sealed record PerfectCastDecision(
    PerfectCastDecisionKind Kind,
    string Reason,
    double PredictedIndicatorPosition);
