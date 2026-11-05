namespace GafiFish.Core.Control;

public enum ReelCommand
{
    Neutral,
    Hold,
    Release
}

public sealed record ReelControlDecision(
    ReelCommand Command,
    double PositionError,
    double ControlSignal,
    string Reason);
