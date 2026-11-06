namespace GafiFish.Core.Automation;

/// <summary>
/// Counts confirmed target catches. Call RecordMatch only after OCR confidence
/// and duplicate checks have passed.
/// </summary>
public sealed class CatchGoalProgress
{
    public CatchGoalProgress(int goal)
    {
        if (goal is < 1 or > 100)
            throw new ArgumentOutOfRangeException(nameof(goal), "Catch goal must be between 1 and 100.");
        Goal = goal;
    }

    public int Goal { get; }
    public int Caught { get; private set; }
    public int Remaining => Math.Max(0, Goal - Caught);
    public bool IsComplete => Caught >= Goal;

    public bool RecordMatch()
    {
        if (!IsComplete)
            Caught++;
        return IsComplete;
    }

    public void Reset() => Caught = 0;
}
