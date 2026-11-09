namespace GafiFish.App.Settings;

public sealed record RerollAutomationSettings
{
    public decimal AppraiseBudget { get; init; } = 100_000m;
    public int AppraiseFishSlot { get; init; } = 1;
    public string AppraiseTargetText { get; init; } = string.Empty;
    public int EnchantRelicLimit { get; init; } = 10;
    public int EnchantRelicSlot { get; init; } = 2;
    public string InteractionKey { get; init; } = "E";
    public string EnchantTargetText { get; init; } = string.Empty;

    public void Validate()
    {
        if (AppraiseBudget is < 1 or > 1_000_000_000m)
            throw new ArgumentOutOfRangeException(nameof(AppraiseBudget),
                "Appraise budget must be between C$ 1 and C$ 1,000,000,000.");

        if (AppraiseFishSlot is < 1 or > 10)
            throw new ArgumentOutOfRangeException(nameof(AppraiseFishSlot),
                "Appraise fish hotbar slot must be from 1 to 0 (10).");

        if (EnchantRelicLimit is < 1 or > 100)
            throw new ArgumentOutOfRangeException(nameof(EnchantRelicLimit),
                "Enchant relic limit must be between 1 and 100.");

        if (EnchantRelicSlot is < 1 or > 10)
            throw new ArgumentOutOfRangeException(nameof(EnchantRelicSlot),
                "Enchant relic hotbar slot must be from 1 to 0 (10).");

        if (InteractionKey is null || InteractionKey.Trim().Length != 1
            || !char.IsLetterOrDigit(InteractionKey.Trim()[0]))
            throw new ArgumentOutOfRangeException(nameof(InteractionKey),
                "Interaction key must be one letter or number.");
        if (AppraiseTargetText is null || AppraiseTargetText.Trim().Length > 80)
            throw new ArgumentOutOfRangeException(nameof(AppraiseTargetText));

        if (EnchantTargetText is null || EnchantTargetText.Trim().Length > 80)
            throw new ArgumentOutOfRangeException(nameof(EnchantTargetText));
    }
}
