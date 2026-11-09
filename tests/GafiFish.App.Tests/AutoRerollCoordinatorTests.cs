using Xunit;
using GafiFish.App.Automation;
using GafiFish.App.Settings;

namespace GafiFish.App.Tests;

public sealed class AutoRerollCoordinatorTests
{
    [Theory]
    [InlineData("C$ 1,234", 1234)]
    [InlineData("$ 2,500", 2500)]
    [InlineData("350 C$", 350)]
    [InlineData("C$ 12.50", 12.50)]
    public void TryParseAppraiseCost_ParsesSingleDisplayedCurrencyValue(string text, decimal expected)
    {
        var parsed = AutoRerollCoordinator.TryParseAppraiseCost(text, out var cost);

        Assert.True(parsed);
        Assert.Equal(expected, cost);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Appraise")]
    [InlineData("C$ unavailable")]
    [InlineData("C$ 100 and C$ 200")]
    [InlineData("100")]
    public void TryParseAppraiseCost_RejectsMissingOrAmbiguousPrice(string text)
    {
        Assert.False(AutoRerollCoordinator.TryParseAppraiseCost(text, out _));
    }

    [Fact]
    public void Settings_DefaultToCappedBudgetAndTenRelics()
    {
        var settings = new RerollAutomationSettings();

        Assert.Equal(100_000m, settings.AppraiseBudget);
        Assert.Equal(10, settings.EnchantRelicLimit);
    }

    [Fact]
    public void Settings_RejectsUnboundedOrInvalidLimits()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RerollAutomationSettings { AppraiseBudget = 0 }.Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RerollAutomationSettings { EnchantRelicLimit = 101 }.Validate());
    }
}
