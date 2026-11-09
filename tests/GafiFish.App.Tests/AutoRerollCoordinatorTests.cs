using System;
using System.Collections.Generic;
using GafiFish.App.Automation;
using GafiFish.App.Settings;
using GafiFish.Ocr.Models;
using GafiFish.Vision.Calibration;
using GafiFish.Windows.Capture;
using GafiFish.Windows.Input;
using Xunit;

namespace GafiFish.App.Tests;

public sealed class AutoRerollCoordinatorTests
{
    private static readonly NormalizedRegion PromptRegion = new(0.05, 0.05, 0.25, 0.20);
    private static readonly NormalizedRegion PriceRegion = new(0.32, 0.05, 0.15, 0.20);
    private static readonly NormalizedRegion ConfirmRegion = new(0.50, 0.05, 0.15, 0.20);
    private static readonly NormalizedRegion ResultRegion = new(0.70, 0.05, 0.25, 0.20);

    [Theory]
    [InlineData("C$ 1,234", 1234)]
    [InlineData("$ 2,500", 2500)]
    [InlineData("350 C$", 350)]
    [InlineData("C$ 12.50", 12.50)]
    [InlineData("1. [Can you appraise this fish? 450C$]", 450)]
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
    public void Appraise_RejectsQuoteAboveBudgetBeforeSelectingOption()
    {
        var input = new FakeRobloxInput();
        var frames = new FrameFactory();
        var ocr = new FakeOcr();
        ocr.Set(ResultRegion, "Ordinary fish · 1.0kg");
        ocr.Set(PromptRegion, "Can you appraise this fish? [500C$]");
        ocr.Set(PriceRegion, "500C$");

        var coordinator = CreateAppraiseCoordinator(input, frames, ocr, budget: 400m);
        coordinator.Start();

        coordinator.ProcessFrame(frames.Next());
        coordinator.ProcessFrame(frames.Next());
        coordinator.ProcessFrame(frames.Next());
        coordinator.ProcessFrame(frames.Next());

        Assert.False(coordinator.IsStarted);
        Assert.Equal(0, coordinator.Attempts);
        Assert.Equal(0m, coordinator.CurrencySpent);
        Assert.Empty(input.Clicks);
        Assert.Contains("Budget protected", coordinator.LastStatus);
        Assert.False(input.IsArmed);
    }

    [Fact]
    public void Appraise_UsesQuoteFromOptionScreenThenConfirmsYesAndStopsOnTarget()
    {
        var input = new FakeRobloxInput();
        var frames = new FrameFactory();
        var ocr = new FakeOcr();
        ocr.Set(ResultRegion, "Ordinary fish · 1.0kg");
        ocr.Set(PromptRegion, "1. Can you appraise this fish? [500C$]");
        ocr.Set(PriceRegion, "500C$");

        var coordinator = CreateAppraiseCoordinator(input, frames, ocr, budget: 1_000m);
        coordinator.Start();

        // Two distinct observations establish the pre-appraisal result.
        coordinator.ProcessFrame(frames.Next());
        coordinator.ProcessFrame(frames.Next());

        // The fee is displayed on the option line, before the Yes/No dialogue.
        coordinator.ProcessFrame(frames.Next());
        Assert.Empty(input.Clicks);
        coordinator.ProcessFrame(frames.Next());
        Assert.Single(input.Clicks);
        Assert.Contains("quoted price C$ 500", coordinator.LastStatus);

        // The follow-up dialogue only needs to prove that Yes is visible.
        ocr.Set(PromptRegion, string.Empty);
        ocr.Set(PriceRegion, string.Empty);
        ocr.Set(ConfirmRegion, "Yes");
        coordinator.ProcessFrame(frames.Next());
        Assert.Single(input.Clicks);
        coordinator.ProcessFrame(frames.Next());

        Assert.Equal(2, input.Clicks.Count);
        Assert.Equal(1, coordinator.Attempts);
        Assert.Equal(500m, coordinator.CurrencySpent);
        Assert.Contains("Waiting for changed fish details", coordinator.LastStatus);

        // A new result is accepted only after two distinct frames.
        ocr.Set(ResultRegion, "Shiny fish · 1.6kg");
        coordinator.ProcessFrame(frames.Next());
        Assert.True(coordinator.IsStarted);
        coordinator.ProcessFrame(frames.Next());

        Assert.False(coordinator.IsStarted);
        Assert.Equal(1, coordinator.Attempts);
        Assert.Equal(500m, coordinator.CurrencySpent);
        Assert.Contains("Appraise target found", coordinator.LastStatus);
        Assert.False(input.IsArmed);
    }

    [Fact]
    public void Appraise_DoesNotSelectOptionWhenPriceIsUnreadableOrAmbiguous()
    {
        var input = new FakeRobloxInput();
        var frames = new FrameFactory();
        var ocr = new FakeOcr();
        ocr.Set(ResultRegion, "Ordinary fish · 1.0kg");
        ocr.Set(PromptRegion, "Can you appraise this fish? [500C$]");
        ocr.Set(PriceRegion, "500C$ / 700C$");

        var coordinator = CreateAppraiseCoordinator(input, frames, ocr, budget: 1_000m);
        coordinator.Start();

        coordinator.ProcessFrame(frames.Next());
        coordinator.ProcessFrame(frames.Next());
        coordinator.ProcessFrame(frames.Next());
        coordinator.ProcessFrame(frames.Next());

        Assert.True(coordinator.IsStarted);
        Assert.Equal(0, coordinator.Attempts);
        Assert.Equal(0m, coordinator.CurrencySpent);
        Assert.Empty(input.Clicks);
        Assert.Contains("readable C$ price", coordinator.LastStatus);
        coordinator.Stop();
        Assert.False(input.IsArmed);
    }

    [Fact]
    public void Enchant_WaitsForStableAltarPromptAndStopsWhenTargetAppears()
    {
        var input = new FakeRobloxInput();
        var frames = new FrameFactory();
        var ocr = new FakeOcr();
        ocr.Set(ResultRegion, "No enchant");
        ocr.Set(PromptRegion, "E Enchant");

        var coordinator = new AutoRerollCoordinator(
            input,
            frames.Fresh,
            ocr.Recognize,
            AutoRerollKind.Enchant,
            PromptRegion,
            ResultRegion,
            "Hasty",
            enchantRelicLimit: 3,
            enchantRelicSlotKey: 0x32,
            interactionKey: 0x45);
        coordinator.Start();

        Assert.Equal(new ushort[] { 0x32 }, input.KeyDowns);

        coordinator.ProcessFrame(frames.Next());
        coordinator.ProcessFrame(frames.Next());

        // One prompt frame is not enough to consume a relic.
        coordinator.ProcessFrame(frames.Next());
        Assert.Equal(new ushort[] { 0x32 }, input.KeyDowns);

        // A second distinct prompt frame permits exactly one E press.
        coordinator.ProcessFrame(frames.Next());
        Assert.Equal(new ushort[] { 0x32, 0x45 }, input.KeyDowns);
        Assert.Equal(1, coordinator.RelicsUsed);

        ocr.Set(ResultRegion, "Hasty");
        coordinator.ProcessFrame(frames.Next());
        Assert.True(coordinator.IsStarted);
        coordinator.ProcessFrame(frames.Next());

        Assert.False(coordinator.IsStarted);
        Assert.Equal(1, coordinator.RelicsUsed);
        Assert.Equal(new ushort[] { 0x32, 0x45 }, input.KeyDowns);
        Assert.Contains("Enchant target found", coordinator.LastStatus);
        Assert.False(input.IsArmed);
    }

    [Fact]
    public void Settings_DefaultToCappedBudgetAndTenRelics()
    {
        var settings = new RerollAutomationSettings();

        Assert.Equal(100_000m, settings.AppraiseBudget);
        Assert.Equal(1, settings.AppraiseFishSlot);
        Assert.Equal(10, settings.EnchantRelicLimit);
        Assert.Equal(2, settings.EnchantRelicSlot);
        Assert.Equal("E", settings.InteractionKey);
    }

    [Fact]
    public void Settings_RejectsUnboundedOrInvalidLimits()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RerollAutomationSettings { AppraiseBudget = 0 }.Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RerollAutomationSettings { EnchantRelicLimit = 101 }.Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RerollAutomationSettings { AppraiseFishSlot = 11 }.Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RerollAutomationSettings { EnchantRelicSlot = 0 }.Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RerollAutomationSettings { InteractionKey = "EE" }.Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RerollAutomationSettings { InteractionKey = "é" }.Validate());
    }

    private static AutoRerollCoordinator CreateAppraiseCoordinator(
        FakeRobloxInput input,
        FrameFactory frames,
        FakeOcr ocr,
        decimal budget) =>
        new(
            input,
            frames.Fresh,
            ocr.Recognize,
            AutoRerollKind.Appraise,
            PromptRegion,
            ResultRegion,
            "Shiny",
            priceRegion: PriceRegion,
            confirmRegion: ConfirmRegion,
            appraiseBudget: budget,
            appraiseFishSlotKey: 0x31,
            interactionKey: 0x45);

    private sealed class FakeRobloxInput : IRobloxInput
    {
        public bool IsArmed { get; private set; }
        public List<ushort> KeyDowns { get; } = [];
        public List<(int X, int Y)> Clicks { get; } = [];

        public void Arm() => IsArmed = true;

        public void SetLeftButton(bool isDown)
        {
            if (!IsArmed && isDown)
                throw new InvalidOperationException("Fake input is disarmed.");
        }

        public void SetKey(ushort virtualKey, bool isDown)
        {
            if (!IsArmed && isDown)
                throw new InvalidOperationException("Fake input is disarmed.");
            if (isDown)
                KeyDowns.Add(virtualKey);
        }

        public void ClickClient(int clientX, int clientY, ScreenCaptureFrame frame)
        {
            if (!IsArmed)
                throw new InvalidOperationException("Fake input is disarmed.");
            if (clientX < 0 || clientX >= frame.Width || clientY < 0 || clientY >= frame.Height)
                throw new ArgumentOutOfRangeException(nameof(clientX));
            Clicks.Add((clientX, clientY));
        }

        public void Disarm() => IsArmed = false;
    }

    private sealed class FakeOcr
    {
        private readonly Dictionary<NormalizedRegion, string> _textByRegion = [];

        public void Set(NormalizedRegion region, string text) => _textByRegion[region] = text;

        public LocalOcrResult Recognize(ScreenCaptureFrame frame, NormalizedRegion region)
        {
            var text = _textByRegion.TryGetValue(region, out var value) ? value : string.Empty;
            return new LocalOcrResult(text, 0.99, region, frame.CapturedAt);
        }
    }

    private sealed class FrameFactory
    {
        private DateTimeOffset _next = DateTimeOffset.UtcNow.AddMilliseconds(-100);

        public ScreenCaptureFrame Next()
        {
            _next = _next.AddMilliseconds(1);
            return Create(_next);
        }

        public ScreenCaptureFrame Fresh() =>
            Create(DateTimeOffset.UtcNow.AddMilliseconds(-2));

        private static ScreenCaptureFrame Create(DateTimeOffset capturedAt) =>
            new(
                Width: 100,
                Height: 100,
                Stride: 400,
                BgraPixels: new byte[40_000],
                CapturedAt: capturedAt,
                WindowTitle: "Roblox",
                ProcessId: 1234);
    }
}
