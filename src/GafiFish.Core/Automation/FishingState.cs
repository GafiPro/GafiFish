namespace GafiFish.Core.Automation;

public enum FishingState
{
    Idle,
    Preparing,
    Casting,
    WaitingForBite,
    Shaking,
    Reeling,
    CatchDetected,
    SellPending,
    OpeningInventory,
    SellingInventory,
    VerifyingSale,
    ClosingInventory,
    Recovering,
    Stopping,
    Stopped,
    Error
}
