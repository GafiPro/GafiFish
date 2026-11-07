namespace GafiFish.Vision.Calibration;

/// <summary>
/// Calibrated, single-click actions that require explicit user consent for
/// each attempt. Treasure Appraiser rows are kept separate so the user picks
/// the exact box rather than having the macro gamble on a random choice.
/// </summary>
public enum FischUiActionKind
{
    AppraiseFish,
    TreasureBoxRow1,
    TreasureBoxRow2,
    TreasureBoxRow3,
    TreasureTakeNow,
    EnchantRod
}

public static class FischUiActionKindExtensions
{
    public static string DisplayName(this FischUiActionKind action) => action switch
    {
        FischUiActionKind.AppraiseFish => "Appraise fish",
        FischUiActionKind.TreasureBoxRow1 => "Treasure Appraiser · row 1 box",
        FischUiActionKind.TreasureBoxRow2 => "Treasure Appraiser · row 2 box",
        FischUiActionKind.TreasureBoxRow3 => "Treasure Appraiser · row 3 box",
        FischUiActionKind.TreasureTakeNow => "Treasure Appraiser · Take Now",
        FischUiActionKind.EnchantRod => "Enchant equipped rod",
        _ => action.ToString()
    };

    public static string SafetyNote(this FischUiActionKind action) => action switch
    {
        FischUiActionKind.AppraiseFish => "Review the selected fish and the displayed fee yourself. Appraise changes fish weight/value and may cost currency. One click only.",
        FischUiActionKind.TreasureBoxRow1 or FischUiActionKind.TreasureBoxRow2 or FischUiActionKind.TreasureBoxRow3 => "Choose the exact box yourself in the visible row, then calibrate only that box. One selection only; the macro does not choose random boxes or continue to another row.",
        FischUiActionKind.TreasureTakeNow => "This action ends the current Treasure Appraiser round. Check the displayed multiplier and confirm that you want to keep it before arming.",
        FischUiActionKind.EnchantRod => "Check the equipped rod and relic yourself. Enchant consumes a relic and applies a random enchantment to the equipped rod; one click only, never chained.",
        _ => "One calibrated click per explicit arming."
    };
}
