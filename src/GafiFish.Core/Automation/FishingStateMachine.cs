namespace GafiFish.Core.Automation;

/// <summary>
/// Validates transitions through the fishing and Sell Anywhere workflows.
/// It intentionally has no input side effects; the automation coordinator
/// owns the input adapter and must release all keys/buttons on stop or error.
/// </summary>
public sealed class FishingStateMachine
{
    public FishingState CurrentState { get; private set; } = FishingState.Idle;
    public DateTimeOffset StateEnteredAt { get; private set; } = DateTimeOffset.UtcNow;
    public string LastTransitionReason { get; private set; } = "Initial state.";

    public bool TryTransition(FishingState nextState, string reason, DateTimeOffset? now = null)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("A transition reason is required.", nameof(reason));
        }

        if (!IsAllowed(CurrentState, nextState))
        {
            return false;
        }

        CurrentState = nextState;
        StateEnteredAt = now ?? DateTimeOffset.UtcNow;
        LastTransitionReason = reason;
        return true;
    }

    public bool HasTimedOut(TimeSpan maximumDuration, DateTimeOffset now)
    {
        if (maximumDuration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumDuration));
        }

        return now - StateEnteredAt > maximumDuration;
    }

    public void Reset(DateTimeOffset? now = null)
    {
        if (CurrentState is not (FishingState.Idle or FishingState.Stopped or FishingState.Error))
        {
            throw new InvalidOperationException("The state machine can only reset while idle, stopped, or in error.");
        }

        CurrentState = FishingState.Idle;
        StateEnteredAt = now ?? DateTimeOffset.UtcNow;
        LastTransitionReason = "Reset.";
    }

    private static bool IsAllowed(FishingState current, FishingState next) =>
        next switch
        {
            FishingState.Stopping => current is not (FishingState.Stopping or FishingState.Stopped),
            FishingState.Stopped => current is FishingState.Stopping or FishingState.Error,
            FishingState.Error => current is not (FishingState.Idle or FishingState.Stopped or FishingState.Error),
            FishingState.Preparing => current is FishingState.Idle
                or FishingState.Reeling
                or FishingState.CatchDetected
                or FishingState.ClosingInventory
                or FishingState.Recovering,
            FishingState.Casting => current is FishingState.Preparing,
            FishingState.WaitingForBite => current is FishingState.Casting,
            FishingState.Shaking => current is FishingState.Preparing or FishingState.WaitingForBite,
            // The visible fish/control markers can confirm that the reel
            // minigame is already active, even if earlier UI states were not seen.
            FishingState.Reeling => current is FishingState.Preparing
                or FishingState.WaitingForBite
                or FishingState.Shaking,
            // A high-confidence OCR notification is a reliable catch boundary
            // even when the intermediate minigame states were not observed.
            FishingState.CatchDetected => current is FishingState.Preparing
                or FishingState.Casting
                or FishingState.WaitingForBite
                or FishingState.Shaking
                or FishingState.Reeling,
            FishingState.SellPending => current is FishingState.CatchDetected,
            FishingState.OpeningInventory => current is FishingState.SellPending,
            FishingState.SellingInventory => current is FishingState.OpeningInventory,
            FishingState.VerifyingSale => current is FishingState.SellingInventory,
            FishingState.ClosingInventory => current is FishingState.VerifyingSale,
            FishingState.Recovering => current is FishingState.Casting
                or FishingState.WaitingForBite
                or FishingState.Shaking
                or FishingState.Reeling
                or FishingState.OpeningInventory
                or FishingState.SellingInventory
                or FishingState.VerifyingSale
                or FishingState.ClosingInventory,
            _ => false
        };
}
