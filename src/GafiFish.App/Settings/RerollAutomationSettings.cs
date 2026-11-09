namespace GafiFish.App.Settings;

public sealed record RerollAutomationSettings
{
    public decimal AppraiseBudget { get; init; } = 100_000m;
    public string AppraiseTargetText { get; init; } = string.Empty;
    public int EnchantRelicLimit { get; init; } = 10;
    public string EnchantTargetText { get; init; } = string.Empty;

    public void Validate()
    {
        if (AppraiseBudget is < 1 or > 1_000_000_000m)
            throw new ArgumentOutOfRangeException(nameof(AppraiseBudget),
                "Appraise budget must be between C$ 1 and C$ 1,000,000,000.");

        if (EnchantRelicLimit is < 1 or > 100)
            throw new ArgumentOutOfRangeException(nameof(EnchantRelicLimit),
                "Enchant relic limit must be between 1 and 100.");

        if (AppraiseTargetText is null || AppraiseTargetText.Trim().Length > 80)
            throw new ArgumentOutOfRangeException(nameof(AppraiseTargetText));

        if (EnchantTargetText is null || EnchantTargetText.Trim().Length > 80)
            throw new ArgumentOutOfRangeException(nameof(EnchantTargetText));
    }
}
