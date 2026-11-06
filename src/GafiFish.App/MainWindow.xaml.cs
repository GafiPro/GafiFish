using System.IO;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.Win32;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using GafiFish.Vision.Calibration;
using GafiFish.Vision.Detection;
using GafiFish.Core.Casting;
using GafiFish.Vision.Tracking;
using GafiFish.Windows.Capture;
using GafiFish.Core.Models;
using GafiFish.Data.Storage;
using GafiFish.Data.Export;
using GafiFish.Ocr.Engine;
using GafiFish.Ocr.Installation;
using GafiFish.Ocr.Parsing;
using GafiFish.Core.Automation;
using GafiFish.Core.Control;
using GafiFish.Core.Scheduling;
using GafiFish.Core.Services;
using GafiFish.App.Automation;
using GafiFish.Windows.Input;

namespace GafiFish.App;

public partial class MainWindow : Window
{
    private const int WmHotkey = 0x0312;
    private const int CaptureHotkeyId = 0x4711;
    private const int PreviewHotkeyId = 0x4712;
    private const int ReelArmHotkeyId = 0x4713;
    private const int EmergencyStopHotkeyId = 0x4714;
    private const uint ModNoRepeat = 0x4000;
    private const uint VkF6 = 0x75;
    private const uint VkF7 = 0x76;
    private const uint VkF8 = 0x77;
    private const uint VkF9 = 0x78;

    private readonly RobloxWindowCapture _capture = new();
    private readonly FishingVisionDetector _vision = new();
    private readonly CastMeterVisionDetector _castVision = new();
    private readonly ShakePromptVisionDetector _shakeVision = new();
    private readonly SellAnywhereVisionDetector _sellVision = new();
    private readonly PerfectCastController _perfectCast = new();
    private readonly FishMotionTracker _motionTracker = new();
    private VisionProfileStore _profileStore = new();
    private string _activeProfileName = "Default";
    private readonly FishSessionStore _sessionStore = new();
    private readonly FishCatchTextParser _catchParser = new();
    private readonly Rectangle _fishMarkerOverlay = new()
    {
        Stroke = Brushes.Lime,
        StrokeThickness = 2,
        IsHitTestVisible = false,
        Visibility = Visibility.Collapsed
    };
    private readonly Rectangle _controlMarkerOverlay = new()
    {
        Stroke = Brushes.DeepSkyBlue,
        StrokeThickness = 2,
        IsHitTestVisible = false,
        Visibility = Visibility.Collapsed
    };
    private readonly Rectangle _castIndicatorOverlay = new()
    {
        Stroke = Brushes.Gold,
        StrokeThickness = 2,
        IsHitTestVisible = false,
        Visibility = Visibility.Collapsed
    };
    private readonly Rectangle _castTargetOverlay = new()
    {
        Stroke = Brushes.Orange,
        StrokeThickness = 2,
        IsHitTestVisible = false,
        Visibility = Visibility.Collapsed
    };
    private readonly Rectangle _sellButtonOverlay = new()
    {
        Stroke = Brushes.OrangeRed,
        StrokeThickness = 2,
        IsHitTestVisible = false,
        Visibility = Visibility.Collapsed
    };
    private readonly List<Rectangle> _shakePromptOverlays = [];

    private VisionProfile _profile = new();
    private ScreenCaptureFrame? _lastFrame;
    private HwndSource? _source;
    private CancellationTokenSource? _previewCancellation;
    private bool _captureHotkeyRegistered;
    private bool _previewHotkeyRegistered;
    private bool _reelArmHotkeyRegistered;
    private bool _emergencyStopHotkeyRegistered;
    private RobloxInputService? _automationInput;
    private ReelInputCoordinator? _reelCoordinator;
    private FishingStateMachine? _runtimeStateMachine;
    private PeriodicSellSchedule? _sellSchedule;
    private SellAnywhereCoordinator? _sellCoordinator;
    private CastInputCoordinator? _castCoordinator;
    private ShakeInputCoordinator? _shakeCoordinator;
    private HotbarItemCoordinator? _hotbarItemCoordinator;
    private TimedHotbarActionSchedule? _timedItemSchedule;
    private int? _activeRodSlot;
    private DateTimeOffset _resumeCastingAfter = DateTimeOffset.MinValue;
    private bool _autoTotemEnabled;
    private bool _autoPotionEnabled;
    private bool _autoCastEnabled;
    private bool _autoShakeEnabled;
    private bool _keepFishingUntilCatchEnabled;
    private bool _targetFishEnabled;
    private string? _targetFishName;
    private bool _startRequested;
    private MissedBiteRetryPolicy _missedBiteRetries = new();
    private bool _automationEnabled;
    private LocalTesseractOcr? _ocr;
    private Guid? _activeSessionId;
    private Guid? _lastCompletedSessionId;
    private bool _ocrInProgress;
    private Task? _catchRecognitionTask;
    private bool _isEndingSession;
    private DateTimeOffset _lastOcrAt = DateTimeOffset.MinValue;
    private DateTimeOffset _lastCatchAt = DateTimeOffset.MinValue;
    private string? _lastCatchFingerprint;
    private DateTimeOffset _lastDisplayedFrameAt = DateTimeOffset.MinValue;
    private double _smoothedCaptureFps;
    private CalibrationAction _calibrationAction;
    private Point? _selectionStartPixel;
    private Point? _selectionStartOverlay;

    private enum CalibrationAction
    {
        None,
        SelectRegion,
        SampleFishMarker,
        SampleControlMarker,
        SelectCastRegion,
        SampleCastIndicator,
        SampleCastTarget,
        SelectShakeRegion,
        SampleShakePrompt,
        SelectSellButtonRegion,
        SampleSellButton,
        SelectInventoryItemsRegion,
        SelectCatchNotificationRegion
    }

    public MainWindow()
    {
        InitializeComponent();
        PreviewOverlay.Children.Add(_fishMarkerOverlay);
        PreviewOverlay.Children.Add(_controlMarkerOverlay);
        PreviewOverlay.Children.Add(_castTargetOverlay);
        PreviewOverlay.Children.Add(_castIndicatorOverlay);
        PreviewOverlay.Children.Add(_sellButtonOverlay);
        Loaded += Window_Loaded;
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        RodProfileNameTextBox.Text = _activeProfileName;
        _profile = await _profileStore.LoadOrDefaultAsync();
        try
        {
            await _sessionStore.InitializeAsync();
        }
        catch (Exception exception)
        {
            SessionStatusText.Text = $"Session database unavailable: {exception.Message}";
        }

        CalibrationModeText.Text = _profile.IsFishingCalibrated
            ? "Saved calibration loaded. Capture a fresh frame to inspect the markers."
            : "No saved marker colors. Select the search region and sample both markers.";
        VisionStatusText.Text = _profile.IsFishingCalibrated
            ? "Vision: saved calibration loaded."
            : "Vision: awaiting color calibration.";
        CastStatusText.Text = _profile.IsCastCalibrated
            ? "Perfect Cast: saved meter calibration loaded."
            : "Perfect Cast: awaiting meter calibration.";
        ShakeStatusText.Text = _profile.IsShakeCalibrated
            ? "Shake: saved prompt calibration loaded."
            : "Shake: awaiting prompt calibration.";
        SellStatusText.Text = _profile.IsSellButtonCalibrated
            ? "Sell Anywhere: saved button calibration loaded."
            : "Sell Anywhere: awaiting button calibration.";
        OcrStatusText.Text = _profile.IsCatchNotificationCalibrated
            ? "Local OCR: catch notification region loaded; start a session to initialise the engine."
            : "Local OCR: select the catch notification region before starting a session.";
        TelemetryText.Text = $"Profile: {_activeProfileName} · waiting for the first frame.";
        UpdateTimedItemControls();
        await RefreshLifetimeStatisticsAsync();
    }

    private async Task RefreshLifetimeStatisticsAsync()
    {
        try
        {
            var statistics = await _sessionStore.GetLifetimeStatisticsAsync();
            var topSpecies = statistics.TopSpecies.Count == 0
                ? "No identified species yet."
                : string.Join(", ", statistics.TopSpecies.Select(item => $"{item.Species} ({item.Count})"));
            var averageWeight = statistics.AverageWeightKg.HasValue
                ? $"{statistics.AverageWeightKg.Value:F2} kg"
                : "n/a";
            var heaviestWeight = statistics.HeaviestWeightKg.HasValue
                ? $"{statistics.HeaviestWeightKg.Value:F2} kg"
                : "n/a";

            LifetimeStatsText.Text =
                $"Sessions: {statistics.TotalSessions} · Catches: {statistics.TotalCatches} " +
                $"({statistics.IdentifiedCatches} identified) · Mutations: {statistics.MutatedCatches} · " +
                $"Legendary+: {statistics.LegendaryOrHigherCatches}{Environment.NewLine}" +
                "Estimated value recognised by OCR: " + statistics.TotalEstimatedValue.ToString("N0") + " · " +
                $"Average weight: {averageWeight} · Heaviest: {heaviestWeight}{Environment.NewLine}" +
                $"Completed fishing time: {statistics.CompletedFishingHours:F1} h · " +
                $"Average: {statistics.CatchesPerHour:F1} catches/h{Environment.NewLine}" +
                $"Top species: {topSpecies}";
        }
        catch (Exception exception)
        {
            LifetimeStatsText.Text = $"Could not load lifetime statistics: {exception.Message}";
        }
    }

    private async void RefreshLifetimeStats_Click(object sender, RoutedEventArgs e)
    {
        await RefreshLifetimeStatisticsAsync();
    }

    private void AlertForCatchIfNeeded(FishCatch fishCatch)
    {
        var watchedSpecies = WatchSpeciesTextBox.Text.Split(
            new[] { ',', ';', '\r', '\n' },
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var options = new CatchAlertOptions(
            watchedSpecies,
            AlertLegendaryCheckBox.IsChecked == true,
            AlertMutationCheckBox.IsChecked == true);
        var reasons = CatchAlertMatcher.GetReasons(fishCatch, options);
        if (reasons.Count == 0)
            return;

        CatchAlertStatusText.Text =
            $"Priority catch: {fishCatch.Species ?? "Unidentified"} · {string.Join(" · ", reasons)}";
        System.Media.SystemSounds.Asterisk.Play();
    }

    private void Window_SourceInitialized(object? sender, EventArgs e)
    {
        var handle = new WindowInteropHelper(this).Handle;
        _source = HwndSource.FromHwnd(handle);
        _source?.AddHook(WindowMessageHook);

        _captureHotkeyRegistered = Native.RegisterHotKey(handle, CaptureHotkeyId, ModNoRepeat, VkF6);
        _previewHotkeyRegistered = Native.RegisterHotKey(handle, PreviewHotkeyId, ModNoRepeat, VkF7);
        _reelArmHotkeyRegistered = Native.RegisterHotKey(handle, ReelArmHotkeyId, ModNoRepeat, VkF8);
        _emergencyStopHotkeyRegistered = Native.RegisterHotKey(handle, EmergencyStopHotkeyId, ModNoRepeat, VkF9);

        HotkeyStatusText.Text =
            $"Global hotkeys — F6 capture: {Registered(_captureHotkeyRegistered)} · " +
            $"F7 observe: {Registered(_previewHotkeyRegistered)} · " +
            $"F8 arm Reel: {Registered(_reelArmHotkeyRegistered)} · " +
            $"F9 emergency stop: {Registered(_emergencyStopHotkeyRegistered)}";
    }

    private IntPtr WindowMessageHook(
        IntPtr hwnd,
        int message,
        IntPtr wParam,
        IntPtr lParam,
        ref bool handled)
    {
        if (message != WmHotkey)
        {
            return IntPtr.Zero;
        }

        var id = wParam.ToInt32();
        if (id == CaptureHotkeyId)
        {
            _ = CaptureOnceAsync();
            handled = true;
        }
        else if (id == PreviewHotkeyId)
        {
            TogglePreviewLoop();
            handled = true;
        }
        else if (id == ReelArmHotkeyId)
        {
            StartAutomation();
            handled = true;
        }
        else if (id == EmergencyStopHotkeyId)
        {
            StopAutomation("Emergency stop (F9): all game inputs released.");
            handled = true;
        }

        return IntPtr.Zero;
    }

    private static string Registered(bool registered) => registered ? "ready" : "in use";

    private void StartMacro_Click(object sender, RoutedEventArgs e)
    {
        if (_automationEnabled || _startRequested)
        {
            return;
        }

        // The button is clicked while this window owns focus. Start observation,
        // then arm only after the capture loop sees Roblox in the foreground.
        if (_previewCancellation is null)
        {
            TogglePreviewLoop();
        }

        _startRequested = true;
        StartMacroButton.IsEnabled = false;
        StopMacroButton.IsEnabled = true;
        AutomationStatusText.Text =
            "Start requested. Switch to the Roblox window; the macro will arm when its first frame is captured.";
    }

    private void StopMacro_Click(object sender, RoutedEventArgs e)
    {
        if (_startRequested && !_automationEnabled)
        {
            _startRequested = false;
            StartMacroButton.IsEnabled = true;
            StopMacroButton.IsEnabled = false;
            AutomationStatusText.Text = "Pending start cancelled.";
            return;
        }

        StopAutomation("Macro stopped by the user; all game inputs released.");
    }

    private void TimedItemCheckBox_Changed(object sender, RoutedEventArgs e) => UpdateTimedItemControls();

    private void UpdateTimedItemControls()
    {
        var enabled = AutoCastCheckBox.IsChecked == true && !_automationEnabled;
        AutoTotemCheckBox.IsEnabled = enabled;
        AutoPotionCheckBox.IsEnabled = enabled;
        TotemSlotComboBox.IsEnabled = enabled && AutoTotemCheckBox.IsChecked == true;
        TotemIntervalTextBox.IsEnabled = enabled && AutoTotemCheckBox.IsChecked == true;
        PotionSlotComboBox.IsEnabled = enabled && AutoPotionCheckBox.IsChecked == true;
        PotionIntervalTextBox.IsEnabled = enabled && AutoPotionCheckBox.IsChecked == true;
    }

    private static int ReadHotbarSlot(ComboBox comboBox, string label)
    {
        var text = (comboBox.SelectedItem as ComboBoxItem)?.Content?.ToString();
        if (text is null || text.Length != 1 || !char.IsDigit(text[0]))
            throw new InvalidOperationException($"Choose a valid hotbar slot for {label}.");
        return text[0] - '0';
    }

    private static TimeSpan ReadItemInterval(TextBox textBox, string label)
    {
        if (!int.TryParse(textBox.Text.Trim(), out var minutes) || minutes is < 1 or > 720)
            throw new InvalidOperationException($"{label} interval must be between 1 and 720 minutes.");
        return TimeSpan.FromMinutes(minutes);
    }

    private void TargetFishCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        TargetFishTextBox.IsEnabled =
            AutoCastCheckBox.IsChecked == true
            && TargetFishCheckBox.IsChecked == true
            && !_automationEnabled;
    }

    private void AutoCastCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (_automationEnabled)
        {
            return;
        }

        var autoCastEnabled = AutoCastCheckBox.IsChecked == true;
        KeepFishingUntilCatchCheckBox.IsEnabled = autoCastEnabled;
        TargetFishCheckBox.IsEnabled = autoCastEnabled;
        TargetFishTextBox.IsEnabled = autoCastEnabled && TargetFishCheckBox.IsChecked == true;
        if (!autoCastEnabled)
        {
            KeepFishingUntilCatchCheckBox.IsChecked = false;
            TargetFishCheckBox.IsChecked = false;
            AutoTotemCheckBox.IsChecked = false;
            AutoPotionCheckBox.IsChecked = false;
        }
        UpdateTimedItemControls();
    }

    private void StartAutomation()
    {
        if (_automationEnabled)
        {
            AutomationStatusText.Text = "Reel assist is already armed. Press F9 to stop it.";
            return;
        }

        _startRequested = false;
        StartMacroButton.IsEnabled = true;
        StopMacroButton.IsEnabled = false;

        if (_previewCancellation is null)
        {
            AutomationStatusText.Text = "Start observation with F7 before arming Reel assist.";
            return;
        }

        if (!_capture.IsRobloxForeground())
        {
            AutomationStatusText.Text = "Focus the Roblox game window, then press F8.";
            return;
        }

        if (!_profile.IsFishingCalibrated)
        {
            AutomationStatusText.Text = "Calibrate the fishing region and both marker colors before arming.";
            return;
        }

        var autoCastEnabled = AutoCastCheckBox.IsChecked == true;
        var autoShakeEnabled = AutoShakeCheckBox.IsChecked == true;
        var autoTotemEnabled = AutoTotemCheckBox.IsChecked == true;
        var autoPotionEnabled = AutoPotionCheckBox.IsChecked == true;
        var keepFishingUntilCatch = KeepFishingUntilCatchCheckBox.IsChecked == true;
        var targetFishEnabled = TargetFishCheckBox.IsChecked == true;
        var targetFishName = TargetFishTextBox.Text.Trim();

        if (targetFishEnabled && string.IsNullOrWhiteSpace(targetFishName))
        {
            AutomationStatusText.Text = "Enter the fish name you want GafiFish to catch.";
            return;
        }

        if (targetFishEnabled && keepFishingUntilCatch)
        {
            AutomationStatusText.Text = "Choose either the first-catch mode or target-fish mode, not both.";
            return;
        }

        if ((autoTotemEnabled || autoPotionEnabled) && !autoCastEnabled)
        {
            AutomationStatusText.Text = "Timed consumables require Auto-cast so they are only used between fishing cycles.";
            return;
        }
        if ((autoTotemEnabled || autoPotionEnabled) && EquipRodCheckBox.IsChecked != true)
        {
            AutomationStatusText.Text = "Timed consumables require Equip my rod so the rod slot can be restored afterwards.";
            return;
        }

        if ((keepFishingUntilCatch || targetFishEnabled) && !autoCastEnabled)
        {
            AutomationStatusText.Text = "Keep-fishing mode requires Auto-cast.";
            return;
        }

        if ((keepFishingUntilCatch || targetFishEnabled)
            && (!_activeSessionId.HasValue || _ocr is null || !_profile.IsCatchNotificationCalibrated))
        {
            AutomationStatusText.Text =
                "First-catch and target-fish modes require an active local OCR session and a calibrated catch-notification region.";
            return;
        }

        if (autoCastEnabled && !_profile.IsCastCalibrated)
        {
            AutomationStatusText.Text = "Auto-cast requires a calibrated Cast region, indicator color and target color.";
            return;
        }

        if (autoShakeEnabled && !_profile.IsShakeCalibrated)
        {
            AutomationStatusText.Text = "Auto-shake requires a calibrated Shake region and prompt color.";
            return;
        }

        RobloxInputService? input = null;
        try
        {
            input = new RobloxInputService(_capture);

            var stateMachine = new FishingStateMachine();
            if (!stateMachine.TryTransition(
                    FishingState.Preparing,
                    "User armed Reel assist with F8.",
                    DateTimeOffset.UtcNow))
            {
                throw new InvalidOperationException("Could not initialise the fishing state machine.");
            }

            var controller = new ReelController(new ReelControlSettings
            {
                PositiveErrorMeansHold = InvertReelDirectionCheckBox.IsChecked != true
            });
            var reelCoordinator = new ReelInputCoordinator(input, controller);
            reelCoordinator.Arm();

            if (EquipRodCheckBox.IsChecked == true)
            {
                var slotText = (RodSlotComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString();
                if (string.IsNullOrWhiteSpace(slotText) || slotText.Length != 1
                    || !char.IsDigit(slotText[0]))
                {
                    throw new InvalidOperationException("Choose a valid hotbar slot from 0 to 9.");
                }

                var virtualKey = (ushort)(0x30 + (slotText[0] - '0'));
                input.SetKey(virtualKey, isDown: true);
                input.SetKey(virtualKey, isDown: false);
            }

            var castCoordinator = autoCastEnabled
                ? new CastInputCoordinator(input, _perfectCast)
                : null;
            var shakeCoordinator = autoShakeEnabled
                ? new ShakeInputCoordinator(input)
                : null;

            var rodSlot = EquipRodCheckBox.IsChecked == true
                ? ReadHotbarSlot(RodSlotComboBox, "rod")
                : (int?)null;
            var timedActions = new List<TimedHotbarAction>();
            if (autoTotemEnabled)
            {
                var slot = ReadHotbarSlot(TotemSlotComboBox, "totem");
                if (slot == rodSlot) throw new InvalidOperationException("Totem slot and rod slot must be different.");
                timedActions.Add(new TimedHotbarAction(TimedHotbarActionKind.Totem, slot, ReadItemInterval(TotemIntervalTextBox, "Totem")));
            }
            if (autoPotionEnabled)
            {
                var slot = ReadHotbarSlot(PotionSlotComboBox, "potion");
                if (slot == rodSlot || timedActions.Any(action => action.HotbarSlot == slot))
                    throw new InvalidOperationException("Potion, totem and rod must use different hotbar slots.");
                timedActions.Add(new TimedHotbarAction(TimedHotbarActionKind.Potion, slot, ReadItemInterval(PotionIntervalTextBox, "Potion")));
            }

            SellAnywhereCoordinator? sellCoordinator = null;
            PeriodicSellSchedule? sellSchedule = null;
            var canScheduleSales =
                SellAnywhereCheckBox.IsChecked == true
                && _activeSessionId.HasValue
                && _profile.IsSellButtonCalibrated
                && _profile.IsInventoryVerificationCalibrated;

            if (canScheduleSales)
            {
                sellSchedule = new PeriodicSellSchedule();
                sellCoordinator = new SellAnywhereCoordinator(
                    input,
                    _sellVision,
                    sellSchedule,
                    stateMachine);
                sellCoordinator.Start(DateTimeOffset.UtcNow);
            }

            _automationInput = input;
            _runtimeStateMachine = stateMachine;
            _reelCoordinator = reelCoordinator;
            _sellCoordinator = sellCoordinator;
            _sellSchedule = sellSchedule;
            _castCoordinator = castCoordinator;
            _shakeCoordinator = shakeCoordinator;
            _hotbarItemCoordinator = timedActions.Count > 0 ? new HotbarItemCoordinator(input) : null;
            _timedItemSchedule = timedActions.Count > 0 ? new TimedHotbarActionSchedule(timedActions, DateTimeOffset.UtcNow) : null;
            _activeRodSlot = rodSlot;
            _resumeCastingAfter = DateTimeOffset.MinValue;
            _autoTotemEnabled = autoTotemEnabled;
            _autoPotionEnabled = autoPotionEnabled;
            _autoCastEnabled = autoCastEnabled;
            _autoShakeEnabled = autoShakeEnabled;
            _keepFishingUntilCatchEnabled = keepFishingUntilCatch;
            _targetFishEnabled = targetFishEnabled;
            _targetFishName = targetFishEnabled ? targetFishName : null;
            _missedBiteRetries = new MissedBiteRetryPolicy(unlimitedRetries: keepFishingUntilCatch || targetFishEnabled);
            _automationEnabled = true;
            StartMacroButton.IsEnabled = false;
            StopMacroButton.IsEnabled = true;
            InvertReelDirectionCheckBox.IsEnabled = false;
            AutoCastCheckBox.IsEnabled = false;
            KeepFishingUntilCatchCheckBox.IsEnabled = false;
            TargetFishCheckBox.IsEnabled = false;
            TargetFishTextBox.IsEnabled = false;
            AutoTotemCheckBox.IsEnabled = false;
            AutoPotionCheckBox.IsEnabled = false;
            TotemSlotComboBox.IsEnabled = false;
            TotemIntervalTextBox.IsEnabled = false;
            PotionSlotComboBox.IsEnabled = false;
            PotionIntervalTextBox.IsEnabled = false;
            AutoShakeCheckBox.IsEnabled = false;
            SellAnywhereCheckBox.IsEnabled = false;
            EquipRodCheckBox.IsEnabled = false;
            RodSlotComboBox.IsEnabled = false;

            var enabledModes = new List<string> { "Reel assist" };
            if (autoCastEnabled) enabledModes.Add("Auto-cast");
            if (keepFishingUntilCatch) enabledModes.Add("until first confirmed catch");
            if (targetFishEnabled) enabledModes.Add($"target: {targetFishName}");
            if (autoTotemEnabled) enabledModes.Add("Auto-totem");
            if (autoPotionEnabled) enabledModes.Add("Auto-potion");
            if (autoShakeEnabled) enabledModes.Add("Auto-shake");
            if (sellCoordinator is not null) enabledModes.Add("Sell Anywhere / 10 min");
            else if (SellAnywhereCheckBox.IsChecked == true)
                enabledModes.Add("Sell Anywhere unavailable (session/calibration missing)");

            AutomationStatusText.Text =
                $"Armed: {string.Join(" + ", enabledModes)}. F9 stops and releases all inputs.";
        }
        catch (Exception exception)
        {
            try
            {
                input?.Disarm();
                input?.Dispose();
            }
            catch
            {
                // Best effort: never hide the original arming error.
            }

            AutomationStatusText.Text = $"Reel assist could not arm: {exception.Message}";
        }
    }

    private void StopAutomation(string reason)
    {
        var releaseFailed = false;

        try
        {
            _sellCoordinator?.Stop();
        }
        catch
        {
            releaseFailed = true;
        }

        try
        {
            _castCoordinator?.Stop();
            _shakeCoordinator?.Reset();
            _reelCoordinator?.Stop();
        }
        catch
        {
            releaseFailed = true;
        }

        // Attempt both paths independently. Dispose performs a second
        // best-effort release if Disarm encountered a rejected Win32 event.
        try
        {
            _automationInput?.Disarm();
        }
        catch
        {
            releaseFailed = true;
        }

        try
        {
            _automationInput?.Dispose();
        }
        catch
        {
            releaseFailed = true;
        }

        if (_runtimeStateMachine is not null
            && _runtimeStateMachine.CurrentState != FishingState.Stopped)
        {
            _ = _runtimeStateMachine.TryTransition(FishingState.Stopping, reason);
            _ = _runtimeStateMachine.TryTransition(
                FishingState.Stopped,
                releaseFailed ? "Stop completed with an input-release warning." : "All inputs released.");
        }

        _automationEnabled = false;
        _startRequested = false;
        _automationInput = null;
        _reelCoordinator = null;
        _runtimeStateMachine = null;
        _sellCoordinator = null;
        _sellSchedule = null;
        _castCoordinator = null;
        _shakeCoordinator = null;
        _hotbarItemCoordinator = null;
        _timedItemSchedule = null;
        _activeRodSlot = null;
        _resumeCastingAfter = DateTimeOffset.MinValue;
        _autoTotemEnabled = false;
        _autoPotionEnabled = false;
        _autoCastEnabled = false;
        _autoShakeEnabled = false;
        _keepFishingUntilCatchEnabled = false;
        _targetFishEnabled = false;
        _targetFishName = null;
        StartMacroButton.IsEnabled = true;
        StopMacroButton.IsEnabled = false;
        InvertReelDirectionCheckBox.IsEnabled = true;
        AutoCastCheckBox.IsEnabled = true;
        KeepFishingUntilCatchCheckBox.IsEnabled = AutoCastCheckBox.IsChecked == true;
        TargetFishCheckBox.IsEnabled = AutoCastCheckBox.IsChecked == true;
        TargetFishTextBox.IsEnabled = AutoCastCheckBox.IsChecked == true && TargetFishCheckBox.IsChecked == true;
        UpdateTimedItemControls();
        AutoShakeCheckBox.IsEnabled = true;
        SellAnywhereCheckBox.IsEnabled = true;
        EquipRodCheckBox.IsEnabled = true;
        RodSlotComboBox.IsEnabled = EquipRodCheckBox.IsChecked == true;
        _missedBiteRetries.Reset();

        AutomationStatusText.Text = releaseFailed
            ? $"{reason} Warning: Windows rejected an input-release event; check the mouse/keyboard state."
            : reason;
    }

    private void ProcessAutomationFrame(
        ScreenCaptureFrame frame,
        DetectedMarker? fishMarker,
        DetectedMarker? controlMarker,
        FishMotionEstimate? motion,
        CastMeterObservation? castObservation,
        IReadOnlyList<DetectedMarker> shakePrompts)
    {
        if (!_automationEnabled
            || _reelCoordinator is null
            || _runtimeStateMachine is null
            || _automationInput is null)
        {
            return;
        }

        try
        {
            if (_sellCoordinator is not null)
            {
                _sellCoordinator.ProcessFrame(
                    frame,
                    _profile,
                    isFishingCycleSafe: _runtimeStateMachine.CurrentState == FishingState.CatchDetected);

                if (!_sellCoordinator.IsStarted)
                {
                    var failure = _sellCoordinator.LastStatus;
                    StopAutomation($"Sell Anywhere stopped safely: {failure}");
                    return;
                }

                if (_runtimeStateMachine.CurrentState == FishingState.CatchDetected
                    && _sellSchedule?.SalePending != true)
                {
                    _ = _runtimeStateMachine.TryTransition(
                        FishingState.Preparing,
                        "No sale is due; resume fishing after the confirmed catch.",
                        frame.CapturedAt);
                }
            }
            else if (_runtimeStateMachine.CurrentState == FishingState.CatchDetected)
            {
                _ = _runtimeStateMachine.TryTransition(
                    FishingState.Preparing,
                    "Catch recorded; prepare for the next fishing cycle.",
                    frame.CapturedAt);
            }

            var castStartedThisFrame = false;

            // A detected fish/bar pair takes priority over Shake candidates.
            var reelMarkersVisible =
                fishMarker is not null
                && controlMarker is not null
                && fishMarker.Confidence >= 0.70
                && controlMarker.Confidence >= 0.70;

            if (reelMarkersVisible
                && (_runtimeStateMachine.CurrentState is
                    FishingState.Preparing or FishingState.WaitingForBite or FishingState.Shaking))
            {
                _shakeCoordinator?.Reset();
                _missedBiteRetries.Reset();
                _ = _runtimeStateMachine.TryTransition(
                    FishingState.Reeling,
                    "Both calibrated reel markers are visible with sufficient confidence.",
                    frame.CapturedAt);
            }

            // Auto-cast is opt-in. The rod remains in the selected hotbar slot.            // Timed consumables run only in Preparing, between completed cycles.
            // One click selects the requested item; the configured rod slot is restored.
            if (_runtimeStateMachine.CurrentState == FishingState.Preparing
                && _timedItemSchedule is not null
                && _hotbarItemCoordinator is not null
                && _activeRodSlot.HasValue)
            {
                if (DateTimeOffset.UtcNow < _resumeCastingAfter)
                    return;

                var dueAction = _timedItemSchedule.TryGetDue(DateTimeOffset.UtcNow, isSafeState: true);
                if (dueAction is not null)
                {
                    _hotbarItemCoordinator.UseItem(dueAction.HotbarSlot, _activeRodSlot.Value);
                    var executedAt = DateTimeOffset.UtcNow;
                    _timedItemSchedule.MarkExecuted(dueAction, executedAt);
                    _resumeCastingAfter = executedAt.AddSeconds(2);
                    AutomationStatusText.Text =
                        $"{dueAction.Kind} clicked from slot {dueAction.HotbarSlot}; rod slot {_activeRodSlot.Value} restored.";
                    return;
                }
            }


            if (_autoCastEnabled
                && _castCoordinator is not null
                && _runtimeStateMachine.CurrentState == FishingState.Preparing
                && !_castCoordinator.IsHolding)
            {
                _castVision.Reset();

                if (!_runtimeStateMachine.TryTransition(
                        FishingState.Casting,
                        "Auto-cast started by the explicitly enabled option.",
                        frame.CapturedAt))
                {
                    throw new InvalidOperationException("Could not enter the Casting state.");
                }

                _castCoordinator.BeginCast(frame.CapturedAt);
                castStartedThisFrame = true;
                AutomationStatusText.Text =
                    "Auto-cast: holding left mouse; waiting for the calibrated Perfect Cast window.";
            }

            if (!castStartedThisFrame
                && _runtimeStateMachine.CurrentState == FishingState.Casting
                && _castCoordinator is not null
                && _castCoordinator.IsHolding)
            {
                var castResult = _castCoordinator.ProcessObservation(
                    castObservation,
                    DateTimeOffset.UtcNow);

                AutomationStatusText.Text = $"Auto-cast: {castResult.Kind} · {castResult.Reason}";

                if (castResult.Kind == CastInputResultKind.Released)
                {
                    if (!_runtimeStateMachine.TryTransition(
                            FishingState.WaitingForBite,
                            "Perfect Cast controller released the mouse inside the calibrated target window.",
                            frame.CapturedAt))
                    {
                        throw new InvalidOperationException("Could not enter WaitingForBite after cast.");
                    }
                }
                else if (castResult.Kind is CastInputResultKind.Cancelled or CastInputResultKind.TimedOut)
                {
                    StopAutomation($"Auto-cast stopped safely: {castResult.Reason}");
                    return;
                }
            }

            // Retry after a quiet cast. The default budget is two; the explicit
            // keep-fishing option removes only this retry cap, not emergency stops.
            if (_autoCastEnabled
                && _runtimeStateMachine.CurrentState == FishingState.WaitingForBite
                && _runtimeStateMachine.HasTimedOut(TimeSpan.FromSeconds(30), frame.CapturedAt))
            {
                if (_missedBiteRetries.TryConsumeRetry())
                {
                    _castVision.Reset();
                    var retryReason = _missedBiteRetries.UnlimitedRetries
                        ? "No bite after 30 seconds; continuing in keep-fishing mode."
                        : $"No bite after 30 seconds; retry {_missedBiteRetries.RetriesUsed}/{_missedBiteRetries.MaximumRetries}.";
                    if (!_runtimeStateMachine.TryTransition(
                            FishingState.Preparing,
                            retryReason,
                            frame.CapturedAt))
                    {
                        throw new InvalidOperationException("Could not return to Preparing after a missed bite.");
                    }

                    AutomationStatusText.Text = _missedBiteRetries.UnlimitedRetries
                        ? "No bite detected. Recasting; unlimited attempts remain enabled until a fish is confirmed."
                        : $"No bite detected. Recasting ({_missedBiteRetries.RetriesUsed}/{_missedBiteRetries.MaximumRetries} retries used).";
                }
                else
                {
                    StopAutomation("No bite was detected after the initial cast and two retries; all inputs released.");
                    return;
                }
            }

            // Process only high-confidence prompts. The state machine enters
            // Shaking after the detector/coordinator confirms an actionable candidate.
            if (_autoShakeEnabled
                && _shakeCoordinator is not null
                && (_runtimeStateMachine.CurrentState is
                    FishingState.Preparing or FishingState.WaitingForBite or FishingState.Shaking))
            {
                var stateBeforeShake = _runtimeStateMachine.CurrentState;
                var shakeResult = _shakeCoordinator.ProcessFrame(frame, shakePrompts);

                if ((stateBeforeShake is FishingState.Preparing or FishingState.WaitingForBite)
                    && shakeResult.Kind == ShakeInputResultKind.Clicked)
                {
                    _ = _runtimeStateMachine.TryTransition(
                        FishingState.Shaking,
                        "A high-confidence Shake prompt remained stable enough to act on.",
                        frame.CapturedAt);
                }
                else if (stateBeforeShake == FishingState.Shaking
                    && shakeResult.Kind == ShakeInputResultKind.PromptSequenceEnded)
                {
                    _ = _runtimeStateMachine.TryTransition(
                        FishingState.WaitingForBite,
                        "Shake prompt sequence ended; waiting for the reel minigame.",
                        frame.CapturedAt);
                }

                if (_runtimeStateMachine.CurrentState == FishingState.Shaking)
                {
                    if (_runtimeStateMachine.HasTimedOut(TimeSpan.FromSeconds(20), frame.CapturedAt))
                    {
                        StopAutomation("Auto-shake timed out; all inputs released.");
                        return;
                    }

                    AutomationStatusText.Text = $"Auto-shake: {shakeResult.Kind} · {shakeResult.Reason}";
                }
            }

            if (_runtimeStateMachine.CurrentState == FishingState.Reeling
                && _runtimeStateMachine.HasTimedOut(TimeSpan.FromMinutes(3), frame.CapturedAt))
            {
                if (_keepFishingUntilCatchEnabled || _targetFishEnabled)
                {
                    var recoveryGoal = _targetFishEnabled
                        ? $"target '{_targetFishName}'"
                        : "the first confirmed catch";
                    if (!_runtimeStateMachine.TryTransition(
                            FishingState.Recovering,
                            "Reel phase exceeded its safety window; release control and begin a fresh cycle.",
                            frame.CapturedAt)
                        || !_runtimeStateMachine.TryTransition(
                            FishingState.Preparing,
                            $"Recovery completed; prepare a fresh cast while waiting for {recoveryGoal}.",
                            frame.CapturedAt))
                    {
                        throw new InvalidOperationException("Could not recover from a timed-out Reel phase.");
                    }

                    _castVision.Reset();
                    _shakeCoordinator?.Reset();
                    _missedBiteRetries.Reset();
                    AutomationStatusText.Text =
                        "Reel safety timeout: control was released and a new fishing cycle will begin.";
                }
                else
                {
                    StopAutomation("The reel minigame exceeded the 3-minute safety limit; all inputs released.");
                    return;
                }
            }

            // Casting owns the left mouse button while it is charging. Reel
            // must not run in parallel and accidentally release the cast.
            if (!(_runtimeStateMachine.CurrentState == FishingState.Casting
                && _castCoordinator?.IsHolding == true))
            {
                var decision = _reelCoordinator.Apply(
                    _runtimeStateMachine.CurrentState,
                    fishMarker,
                    controlMarker,
                    motion);

                if (_runtimeStateMachine.CurrentState == FishingState.Reeling)
                {
                    AutomationStatusText.Text =
                        $"Reel assist: {_runtimeStateMachine.CurrentState} · {decision.Command} · {decision.Reason}";
                }
            }
        }
        catch (Exception exception)
        {
            StopAutomation($"Fishing automation stopped safely: {exception.Message}");
        }
    }

    private async void CaptureOnce_Click(object sender, RoutedEventArgs e)
    {
        await CaptureOnceAsync();
    }

    private async Task CaptureOnceAsync()
    {
        try
        {
            StatusText.Text = "Capturing visible Roblox client…";
            var frame = await Task.Run(_capture.CaptureForegroundClient);
            DisplayFrame(frame);
            StatusText.Text = $"Captured {frame.WindowTitle} (PID {frame.ProcessId}).";
        }
        catch (Exception exception) when (
            exception is InvalidOperationException
            or System.ComponentModel.Win32Exception
            or ArgumentException)
        {
            StatusText.Text = exception.Message;
        }
    }

    private void TogglePreview_Click(object sender, RoutedEventArgs e)
    {
        TogglePreviewLoop();
    }

    private void TogglePreviewLoop()
    {
        if (_previewCancellation is not null)
        {
            StopPreviewLoop();
            return;
        }

        _previewCancellation = new CancellationTokenSource();
        PreviewButton.Content = "Stop observation (F7)";
        StatusText.Text = "Observation active. Bring Roblox to the foreground.";
        _ = RunPreviewLoopAsync(_previewCancellation);
    }

    private void StopPreviewLoop()
    {
        if (_automationEnabled)
        {
            StopAutomation("Observation stopped; Reel assist disarmed and all inputs released.");
        }
        else if (_startRequested)
        {
            _startRequested = false;
            StartMacroButton.IsEnabled = true;
            StopMacroButton.IsEnabled = false;
            AutomationStatusText.Text = "Observation stopped; pending macro start cancelled.";
        }

        _previewCancellation?.Cancel();
        PreviewButton.Content = "Start observation (F7)";
        StatusText.Text = "Observation stopped.";
    }

    private async Task RunPreviewLoopAsync(CancellationTokenSource owner)
    {
        var token = owner.Token;
        try
        {
            while (!token.IsCancellationRequested)
            {
                var loopStartedAt = Stopwatch.GetTimestamp();
                if (_capture.IsRobloxForeground())
                {
                    try
                    {
                        var frame = await Task.Run(_capture.CaptureForegroundClient, token);
                        DisplayFrame(frame);
                        StatusText.Text = "Observing Roblox. Alt+Tab here to inspect the latest frame.";

                        if (_startRequested && !_automationEnabled)
                        {
                            _startRequested = false;
                            StartMacroButton.IsEnabled = true;
                            StopMacroButton.IsEnabled = false;
                            StartAutomation();
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception exception) when (
                        exception is InvalidOperationException
                        or System.ComponentModel.Win32Exception
                        or ArgumentException)
                    {
                        StatusText.Text = $"Capture paused: {exception.Message}";
                        if (_automationEnabled)
                        {
                            StopAutomation($"Screen capture failed; all game inputs were released: {exception.Message}");
                        }
                    }
                }
                else if (_automationEnabled)
                {
                    StopAutomation("Roblox lost foreground focus; all Reel inputs were released.");
                }

                var processingMilliseconds = Stopwatch.GetElapsedTime(loopStartedAt).TotalMilliseconds;
                var adaptiveDelayMilliseconds = (int)Math.Clamp(120 - processingMilliseconds, 15, 120);
                await Task.Delay(adaptiveDelayMilliseconds, token);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal stop path.
        }
        finally
        {
            if (ReferenceEquals(_previewCancellation, owner))
            {
                _previewCancellation = null;
                owner.Dispose();
                PreviewButton.Content = "Start observation (F7)";
            }
        }
    }

    private void DisplayFrame(ScreenCaptureFrame frame)
    {
        var previousFrameAt = _lastDisplayedFrameAt;
        _lastDisplayedFrameAt = frame.CapturedAt;
        if (previousFrameAt != DateTimeOffset.MinValue)
        {
            var delta = (frame.CapturedAt - previousFrameAt).TotalSeconds;
            if (delta > 0 && double.IsFinite(delta))
            {
                var instantFps = 1d / delta;
                _smoothedCaptureFps = _smoothedCaptureFps <= 0
                    ? instantFps
                    : (_smoothedCaptureFps * 0.8) + (instantFps * 0.2);
            }
        }
        _lastFrame = frame;

        var bitmap = BitmapSource.Create(
            frame.Width,
            frame.Height,
            96,
            96,
            PixelFormats.Bgra32,
            null,
            frame.BgraPixels,
            frame.Stride);
        bitmap.Freeze();

        PreviewImage.Source = bitmap;
        PreviewPlaceholder.Visibility = Visibility.Collapsed;
        ResolutionText.Text = $"{frame.Width} × {frame.Height}";

        var visionResult = _vision.Detect(frame, _profile);
        DrawMarker(_fishMarkerOverlay, visionResult.FishMarker);
        DrawMarker(_controlMarkerOverlay, visionResult.ControlMarker);

        var motion = _motionTracker.Update(visionResult.FishMarker, frame.CapturedAt);
        VisionStatusText.Text = motion is not null && visionResult.FishMarker is not null
            ? $"Vision: {visionResult.Status} Fish X={motion.PositionX:F0}px · {motion.VelocityPixelsPerSecond:+0.0;-0.0;0} px/s · confidence {motion.Confidence:P0}"
            : $"Vision: {visionResult.Status}";
        var fishX = motion is null ? "—" : $"{motion.PositionX:F0}px";
        var controlX = visionResult.ControlMarker is null ? "—" : $"{visionResult.ControlMarker.CenterX:F0}px";
        var error = motion is null || visionResult.ControlMarker is null
            ? "—"
            : $"{motion.PositionX - visionResult.ControlMarker.CenterX:+0;-0;0}px";
        var state = _runtimeStateMachine?.CurrentState.ToString() ?? "Disarmed";
        TelemetryText.Text =
            $"Telemetry · {state} · Fish {fishX} · Control {controlX} · Error {error} · " +
            $"Confidence {(motion?.Confidence ?? 0):P0} · Capture {_smoothedCaptureFps:F1} FPS · Profile {_activeProfileName}";

        var castObservation = _castVision.Detect(frame, _profile);
        if (castObservation is null)
        {
            CastStatusText.Text = _profile.IsCastCalibrated
                ? "Perfect Cast: meter markers not detected."
                : "Perfect Cast: select the meter region and sample both colors.";
        }
        else
        {
            var castDecision = _perfectCast.Evaluate(castObservation, DateTimeOffset.UtcNow);
            CastStatusText.Text = $"Perfect Cast: {castDecision.Kind} · position {castObservation.IndicatorPosition:P0} · velocity {castObservation.IndicatorVelocityPerSecond:+0.00;-0.00;0.00}/s · {castDecision.Reason}";

            var castRegion = _profile.CastRegion.ToPixelRect(frame.Width, frame.Height);
            var targetLeft = castRegion.X + (int)Math.Floor(castObservation.TargetStart * castRegion.Width);
            var targetRight = castRegion.X + (int)Math.Ceiling(castObservation.TargetEnd * castRegion.Width);
            DrawPixelRectOverlay(
                _castTargetOverlay,
                new OpenCvSharp.Rect(targetLeft, castRegion.Y, Math.Max(1, targetRight - targetLeft), castRegion.Height));

            var indicatorX = castRegion.X + (int)Math.Round(castObservation.IndicatorPosition * castRegion.Width);
            DrawPixelRectOverlay(
                _castIndicatorOverlay,
                new OpenCvSharp.Rect(indicatorX - 1, castRegion.Y, 3, castRegion.Height));
        }

        if (castObservation is null)
        {
            _castIndicatorOverlay.Visibility = Visibility.Collapsed;
            _castTargetOverlay.Visibility = Visibility.Collapsed;
        }

        var shakeResult = _shakeVision.Detect(frame, _profile);
        ShakeStatusText.Text = shakeResult.IsCalibrated
            ? $"Shake: {shakeResult.Status}"
            : "Shake: select the prompt region and sample its color.";
        DrawDetectedMarkers(_shakePromptOverlays, shakeResult.Prompts, Brushes.Orange, maxMarkers: 8);

        var sellResult = _sellVision.DetectButton(frame, _profile);
        SellStatusText.Text = sellResult.IsCalibrated
            ? $"Sell Anywhere: {sellResult.Status}"
            : "Sell Anywhere: select the button region and sample its color.";
        DrawMarker(_sellButtonOverlay, sellResult.SellButton);

        ProcessAutomationFrame(
            frame,
            visionResult.FishMarker,
            visionResult.ControlMarker,
            motion,
            castObservation,
            shakeResult.Prompts);

        TryStartCatchRecognition(frame);
    }

    private void DrawDetectedMarkers(
        List<Rectangle> overlays,
        IReadOnlyList<DetectedMarker> markers,
        Brush stroke,
        int maxMarkers)
    {
        var visibleCount = Math.Min(markers.Count, maxMarkers);
        while (overlays.Count < visibleCount)
        {
            var overlay = new Rectangle
            {
                Stroke = stroke,
                StrokeThickness = 2,
                IsHitTestVisible = false,
                Visibility = Visibility.Collapsed
            };
            overlays.Add(overlay);
            PreviewOverlay.Children.Add(overlay);
        }

        for (var i = 0; i < overlays.Count; i++)
        {
            if (i >= visibleCount)
            {
                overlays[i].Visibility = Visibility.Collapsed;
                continue;
            }

            overlays[i].Stroke = stroke;
            DrawMarker(overlays[i], markers[i]);
        }
    }

    private void DrawPixelRectOverlay(Rectangle overlay, OpenCvSharp.Rect pixelBounds)
    {
        if (PreviewImage.Source is not BitmapSource bitmap
            || pixelBounds.Width <= 0
            || pixelBounds.Height <= 0)
        {
            overlay.Visibility = Visibility.Collapsed;
            return;
        }

        var scale = GetImageScale(
            bitmap.PixelWidth,
            bitmap.PixelHeight,
            out var offsetX,
            out var offsetY);

        Canvas.SetLeft(overlay, offsetX + pixelBounds.X * scale);
        Canvas.SetTop(overlay, offsetY + pixelBounds.Y * scale);
        overlay.Width = Math.Max(2, pixelBounds.Width * scale);
        overlay.Height = Math.Max(2, pixelBounds.Height * scale);
        overlay.Visibility = Visibility.Visible;
    }

    private void DrawMarker(Rectangle overlay, DetectedMarker? marker)
    {
        if (marker is null || PreviewImage.Source is not BitmapSource bitmap)
        {
            overlay.Visibility = Visibility.Collapsed;
            return;
        }

        var scale = GetImageScale(bitmap.PixelWidth, bitmap.PixelHeight, out var offsetX, out var offsetY);
        Canvas.SetLeft(overlay, offsetX + marker.Bounds.X * scale);
        Canvas.SetTop(overlay, offsetY + marker.Bounds.Y * scale);
        overlay.Width = Math.Max(2, marker.Bounds.Width * scale);
        overlay.Height = Math.Max(2, marker.Bounds.Height * scale);
        overlay.Visibility = Visibility.Visible;
    }

    private double GetImageScale(int pixelWidth, int pixelHeight, out double offsetX, out double offsetY)
    {
        var areaWidth = PreviewOverlay.ActualWidth;
        var areaHeight = PreviewOverlay.ActualHeight;
        var scale = Math.Min(areaWidth / pixelWidth, areaHeight / pixelHeight);
        var renderedWidth = pixelWidth * scale;
        var renderedHeight = pixelHeight * scale;
        offsetX = (areaWidth - renderedWidth) / 2;
        offsetY = (areaHeight - renderedHeight) / 2;
        return scale;
    }

    private bool TryMapOverlayPoint(Point overlayPoint, out Point pixelPoint)
    {
        pixelPoint = default;
        if (_lastFrame is null || PreviewImage.Source is not BitmapSource bitmap)
        {
            return false;
        }

        var scale = GetImageScale(bitmap.PixelWidth, bitmap.PixelHeight, out var offsetX, out var offsetY);
        var localX = overlayPoint.X - offsetX;
        var localY = overlayPoint.Y - offsetY;
        var renderedWidth = bitmap.PixelWidth * scale;
        var renderedHeight = bitmap.PixelHeight * scale;

        if (localX < 0 || localY < 0 || localX >= renderedWidth || localY >= renderedHeight)
        {
            return false;
        }

        pixelPoint = new Point(
            Math.Clamp(localX / scale, 0, bitmap.PixelWidth - 1),
            Math.Clamp(localY / scale, 0, bitmap.PixelHeight - 1));
        return true;
    }

    private static string ProfileSlug(string name)
    {
        var slug = new string(name.Trim().ToLowerInvariant()
            .Select(ch => char.IsLetterOrDigit(ch) ? ch : '-').ToArray());
        while (slug.Contains("--", StringComparison.Ordinal))
            slug = slug.Replace("--", "-", StringComparison.Ordinal);
        slug = slug.Trim('-');
        if (slug.Length is < 2 or > 48)
            throw new InvalidOperationException("Profile name must contain 2–48 letters or numbers.");
        return slug;
    }

    private static VisionProfileStore CreateProfileStore(string name)
    {
        if (string.Equals(name.Trim(), "Default", StringComparison.OrdinalIgnoreCase))
            return new VisionProfileStore();
        var path = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "GafiFish", "rod-profiles", $"{ProfileSlug(name)}.json");
        return new VisionProfileStore(path);
    }

    private async void SaveRodProfile_Click(object sender, RoutedEventArgs e)
    {
        if (_automationEnabled)
        {
            CalibrationModeText.Text = "Stop the macro before saving a profile.";
            return;
        }
        try
        {
            _activeProfileName = RodProfileNameTextBox.Text.Trim();
            _profileStore = CreateProfileStore(_activeProfileName);
            await SaveProfileAsync($"Calibration saved to profile '{_activeProfileName}'.");
            TelemetryText.Text = $"Profile: {_activeProfileName} · saved.";
        }
        catch (Exception exception)
        {
            CalibrationModeText.Text = $"Could not save profile: {exception.Message}";
        }
    }

    private async void LoadRodProfile_Click(object sender, RoutedEventArgs e)
    {
        if (_automationEnabled)
        {
            CalibrationModeText.Text = "Stop the macro before loading a profile.";
            return;
        }
        try
        {
            _activeProfileName = RodProfileNameTextBox.Text.Trim();
            _profileStore = CreateProfileStore(_activeProfileName);
            _profile = await _profileStore.LoadOrDefaultAsync();
            _motionTracker.Reset();
            _castVision.Reset();
            CalibrationModeText.Text = $"Loaded profile '{_activeProfileName}'.";
            VisionStatusText.Text = _profile.IsFishingCalibrated ? "Vision: profile calibration loaded." : "Vision: profile needs fishing calibration.";
            CastStatusText.Text = _profile.IsCastCalibrated ? "Perfect Cast: profile calibration loaded." : "Perfect Cast: awaiting meter calibration.";
            ShakeStatusText.Text = _profile.IsShakeCalibrated ? "Shake: profile calibration loaded." : "Shake: awaiting prompt calibration.";
            SellStatusText.Text = _profile.IsSellButtonCalibrated ? "Sell Anywhere: profile calibration loaded." : "Sell Anywhere: awaiting button calibration.";
            OcrStatusText.Text = _profile.IsCatchNotificationCalibrated ? "Local OCR: catch notification region loaded." : "Local OCR: select the catch notification region before starting a session.";
            TelemetryText.Text = $"Profile: {_activeProfileName} · loaded.";
        }
        catch (Exception exception)
        {
            CalibrationModeText.Text = $"Could not load profile: {exception.Message}";
        }
    }

    private void SelectRegion_Click(object sender, RoutedEventArgs e)
    {
        SetCalibrationAction(CalibrationAction.SelectRegion,
            "Drag a rectangle around the fishing minigame on the captured frame.");
    }

    private void SampleFishMarker_Click(object sender, RoutedEventArgs e)
    {
        SetCalibrationAction(CalibrationAction.SampleFishMarker,
            "Click a clear pixel inside the moving fish marker on the captured frame.");
    }

    private void SampleControlMarker_Click(object sender, RoutedEventArgs e)
    {
        SetCalibrationAction(CalibrationAction.SampleControlMarker,
            "Click a clear pixel inside your control marker on the captured frame.");
    }

    private void SelectCastRegion_Click(object sender, RoutedEventArgs e)
    {
        SetCalibrationAction(CalibrationAction.SelectCastRegion,
            "Drag a rectangle around the Perfect Cast meter on a frame where it is visible.");
    }

    private void SampleCastIndicator_Click(object sender, RoutedEventArgs e)
    {
        SetCalibrationAction(CalibrationAction.SampleCastIndicator,
            "Click a clear pixel inside the moving Perfect Cast indicator.");
    }

    private void SampleCastTarget_Click(object sender, RoutedEventArgs e)
    {
        SetCalibrationAction(CalibrationAction.SampleCastTarget,
            "Click a clear pixel inside the green/target area of the Perfect Cast meter.");
    }

    private void SelectShakeRegion_Click(object sender, RoutedEventArgs e)
    {
        SetCalibrationAction(CalibrationAction.SelectShakeRegion,
            "Drag a rectangle around the Shake prompt area on a frame where it is visible.");
    }

    private void SampleShakePrompt_Click(object sender, RoutedEventArgs e)
    {
        SetCalibrationAction(CalibrationAction.SampleShakePrompt,
            "Click a clear pixel inside the visible Shake prompt.");
    }

    private void SelectSellButtonRegion_Click(object sender, RoutedEventArgs e)
    {
        SetCalibrationAction(CalibrationAction.SelectSellButtonRegion,
            "Drag a rectangle around the Sell Inventory button inside the open inventory.");
    }

    private void SampleSellButton_Click(object sender, RoutedEventArgs e)
    {
        SetCalibrationAction(CalibrationAction.SampleSellButton,
            "Click a clear pixel inside the Sell Inventory button.");
    }

    private void SelectInventoryItemsRegion_Click(object sender, RoutedEventArgs e)
    {
        SetCalibrationAction(CalibrationAction.SelectInventoryItemsRegion,
            "Drag a rectangle around the inventory item list. This area is used to verify that the list changes after selling.");
    }

    private void SelectCatchNotificationRegion_Click(object sender, RoutedEventArgs e)
    {
        SetCalibrationAction(CalibrationAction.SelectCatchNotificationRegion,
            "Drag a rectangle around the fish-caught notification. Keep the rectangle tight around the notification text.");
    }

    private void EquipRodCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (!_automationEnabled)
        {
            RodSlotComboBox.IsEnabled = EquipRodCheckBox.IsChecked == true;
        }
    }

    private void SetCalibrationAction(CalibrationAction action, string instruction)
    {
        if (_automationEnabled)
        {
            StopAutomation("Calibration changed; Reel assist was disarmed and all inputs released.");
        }

        if (_lastFrame is null)
        {
            CalibrationModeText.Text = "Capture a frame with F6 before calibrating.";
            return;
        }

        _calibrationAction = action;
        CalibrationModeText.Text = instruction;
        SelectionRectangle.Visibility = Visibility.Collapsed;
    }

    private void PreviewOverlay_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var overlayPoint = e.GetPosition(PreviewOverlay);
        if (!TryMapOverlayPoint(overlayPoint, out var pixelPoint) || _lastFrame is null)
        {
            return;
        }

        if (_calibrationAction is CalibrationAction.SampleFishMarker
            or CalibrationAction.SampleControlMarker
            or CalibrationAction.SampleCastIndicator
            or CalibrationAction.SampleCastTarget
            or CalibrationAction.SampleShakePrompt
            or CalibrationAction.SampleSellButton)
        {
            SampleMarkerColor(pixelPoint);
            e.Handled = true;
            return;
        }

        if (_calibrationAction is CalibrationAction.SelectRegion
            or CalibrationAction.SelectCastRegion
            or CalibrationAction.SelectShakeRegion
            or CalibrationAction.SelectSellButtonRegion
            or CalibrationAction.SelectInventoryItemsRegion
            or CalibrationAction.SelectCatchNotificationRegion)
        {
            _selectionStartPixel = pixelPoint;
            _selectionStartOverlay = overlayPoint;
            SelectionRectangle.Visibility = Visibility.Visible;
            SelectionRectangle.Width = 1;
            SelectionRectangle.Height = 1;
            Canvas.SetLeft(SelectionRectangle, overlayPoint.X);
            Canvas.SetTop(SelectionRectangle, overlayPoint.Y);
            PreviewOverlay.CaptureMouse();
            e.Handled = true;
        }
    }

    private void PreviewOverlay_MouseMove(object sender, MouseEventArgs e)
    {
        if (_calibrationAction is not (CalibrationAction.SelectRegion
            or CalibrationAction.SelectCastRegion
            or CalibrationAction.SelectShakeRegion
            or CalibrationAction.SelectSellButtonRegion
            or CalibrationAction.SelectInventoryItemsRegion
            or CalibrationAction.SelectCatchNotificationRegion)
            || _selectionStartOverlay is null
            || !PreviewOverlay.IsMouseCaptured)
        {
            return;
        }

        var current = e.GetPosition(PreviewOverlay);
        var start = _selectionStartOverlay.Value;
        Canvas.SetLeft(SelectionRectangle, Math.Min(start.X, current.X));
        Canvas.SetTop(SelectionRectangle, Math.Min(start.Y, current.Y));
        SelectionRectangle.Width = Math.Abs(current.X - start.X);
        SelectionRectangle.Height = Math.Abs(current.Y - start.Y);
    }

    private async void PreviewOverlay_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_calibrationAction is not (CalibrationAction.SelectRegion
            or CalibrationAction.SelectCastRegion
            or CalibrationAction.SelectShakeRegion
            or CalibrationAction.SelectSellButtonRegion
            or CalibrationAction.SelectInventoryItemsRegion
            or CalibrationAction.SelectCatchNotificationRegion)
            || _selectionStartPixel is null
            || _lastFrame is null)
        {
            return;
        }

        var endOverlay = e.GetPosition(PreviewOverlay);
        if (!TryMapOverlayPoint(endOverlay, out var endPixel))
        {
            PreviewOverlay.ReleaseMouseCapture();
            ClearRegionSelection();
            return;
        }

        var startPixel = _selectionStartPixel.Value;
        var left = Math.Min(startPixel.X, endPixel.X);
        var top = Math.Min(startPixel.Y, endPixel.Y);
        var width = Math.Abs(endPixel.X - startPixel.X);
        var height = Math.Abs(endPixel.Y - startPixel.Y);

        PreviewOverlay.ReleaseMouseCapture();
        ClearRegionSelection();

        if (width < 8 || height < 8)
        {
            CalibrationModeText.Text = "Region too small. Draw a larger rectangle.";
            return;
        }

        var selectedRegion = new NormalizedRegion(
            left / _lastFrame.Width,
            top / _lastFrame.Height,
            width / _lastFrame.Width,
            height / _lastFrame.Height);

        var selectedAction = _calibrationAction;
        _profile = selectedAction switch
        {
            CalibrationAction.SelectRegion => _profile with
            {
                FishingRegion = selectedRegion,
                FishingRegionCalibrated = true,
                ReferenceWidth = _lastFrame.Width,
                ReferenceHeight = _lastFrame.Height
            },
            CalibrationAction.SelectCastRegion => _profile with
            {
                CastRegion = selectedRegion,
                CastRegionCalibrated = true,
                ReferenceWidth = _lastFrame.Width,
                ReferenceHeight = _lastFrame.Height
            },
            CalibrationAction.SelectShakeRegion => _profile with
            {
                ShakeRegion = selectedRegion,
                ShakeRegionCalibrated = true,
                ReferenceWidth = _lastFrame.Width,
                ReferenceHeight = _lastFrame.Height
            },
            CalibrationAction.SelectSellButtonRegion => _profile with
            {
                SellButtonRegion = selectedRegion,
                SellButtonRegionCalibrated = true,
                ReferenceWidth = _lastFrame.Width,
                ReferenceHeight = _lastFrame.Height
            },
            CalibrationAction.SelectInventoryItemsRegion => _profile with
            {
                InventoryItemsRegion = selectedRegion,
                InventoryItemsRegionCalibrated = true,
                ReferenceWidth = _lastFrame.Width,
                ReferenceHeight = _lastFrame.Height
            },
            CalibrationAction.SelectCatchNotificationRegion => _profile with
            {
                CatchNotificationRegion = selectedRegion,
                CatchNotificationRegionCalibrated = true,
                ReferenceWidth = _lastFrame.Width,
                ReferenceHeight = _lastFrame.Height
            },
            _ => _profile
        };

        _motionTracker.Reset();
        _castVision.Reset();
        var regionMessage = selectedAction switch
        {
            CalibrationAction.SelectRegion => "Fishing search region saved.",
            CalibrationAction.SelectCastRegion => "Perfect Cast meter region saved.",
            CalibrationAction.SelectShakeRegion => "Shake prompt region saved.",
            CalibrationAction.SelectSellButtonRegion => "Sell Anywhere button region saved.",
            CalibrationAction.SelectInventoryItemsRegion => "Inventory item-list region saved.",
            CalibrationAction.SelectCatchNotificationRegion => "Catch notification OCR region saved.",
            _ => "Region saved."
        };
        await SaveProfileAsync(regionMessage);
        _calibrationAction = CalibrationAction.None;
        e.Handled = true;
    }

    private void ClearRegionSelection()
    {
        _selectionStartPixel = null;
        _selectionStartOverlay = null;
        SelectionRectangle.Visibility = Visibility.Collapsed;
        SelectionRectangle.Width = 0;
        SelectionRectangle.Height = 0;
    }

    private async void SampleMarkerColor(Point pixelPoint)
    {
        if (_lastFrame is null)
        {
            return;
        }

        var x = Math.Clamp((int)Math.Round(pixelPoint.X), 0, _lastFrame.Width - 1);
        var y = Math.Clamp((int)Math.Round(pixelPoint.Y), 0, _lastFrame.Height - 1);
        var index = y * _lastFrame.Stride + x * 4;

        var color = HsvColorSampler.FromBgra(
            _lastFrame.BgraPixels[index],
            _lastFrame.BgraPixels[index + 1],
            _lastFrame.BgraPixels[index + 2]);

        _profile = _calibrationAction switch
        {
            CalibrationAction.SampleFishMarker => _profile with { FishMarkerColor = color },
            CalibrationAction.SampleControlMarker => _profile with { ControlMarkerColor = color },
            CalibrationAction.SampleCastIndicator => _profile with { CastIndicatorColor = color },
            CalibrationAction.SampleCastTarget => _profile with { CastTargetColor = color },
            CalibrationAction.SampleShakePrompt => _profile with { ShakePromptColor = color },
            CalibrationAction.SampleSellButton => _profile with { SellButtonColor = color },
            _ => _profile
        };

        _profile = _profile with
        {
            ReferenceWidth = _lastFrame.Width,
            ReferenceHeight = _lastFrame.Height
        };

        _calibrationAction = CalibrationAction.None;
        _motionTracker.Reset();
        await SaveProfileAsync("Marker color saved.");
    }

    private async void ResetCalibration_Click(object sender, RoutedEventArgs e)
    {
        if (_automationEnabled)
        {
            StopAutomation("Calibration reset; Reel assist was disarmed and all inputs released.");
        }

        _profile = new VisionProfile();
        _motionTracker.Reset();
        _fishMarkerOverlay.Visibility = Visibility.Collapsed;
        _controlMarkerOverlay.Visibility = Visibility.Collapsed;
        _calibrationAction = CalibrationAction.None;
        await SaveProfileAsync("Calibration reset.");
        VisionStatusText.Text = "Vision: awaiting color calibration.";
        CastStatusText.Text = "Perfect Cast: awaiting meter calibration.";
        ShakeStatusText.Text = "Shake: awaiting prompt calibration.";
        SellStatusText.Text = "Sell Anywhere: awaiting button calibration.";
        OcrStatusText.Text = "Local OCR: select the catch notification region before starting a session.";
    }

    private async Task SaveProfileAsync(string successMessage)
    {
        try
        {
            await _profileStore.SaveAsync(_profile);
            CalibrationModeText.Text = successMessage;
            if (_lastFrame is not null)
            {
                var result = _vision.Detect(_lastFrame, _profile);
                VisionStatusText.Text = $"Vision: {result.Status}";
                var cast = _castVision.Detect(_lastFrame, _profile);
                CastStatusText.Text = cast is null
                    ? "Perfect Cast: meter not detected or not calibrated."
                    : $"Perfect Cast: { _perfectCast.Evaluate(cast, DateTimeOffset.UtcNow).Kind }.";
            }
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or ArgumentOutOfRangeException)
        {
            CalibrationModeText.Text = $"Could not save calibration: {exception.Message}";
        }
    }

    private async void StartSession_Click(object sender, RoutedEventArgs e)
    {
        if (_activeSessionId.HasValue)
        {
            return;
        }

        if (!_profile.IsCatchNotificationCalibrated)
        {
            SessionStatusText.Text = "Select the catch notification region in Visual calibration first.";
            return;
        }

        StartSessionButton.IsEnabled = false;
        SessionStatusText.Text = "Preparing local OCR and session database…";
        OcrStatusText.Text = "Checking the English OCR model…";

        try
        {
            await _sessionStore.InitializeAsync();
            var tessdataDirectory = await TesseractModelInstaller.EnsureEnglishModelAsync();

            _ocr ??= await Task.Run(() => new LocalTesseractOcr(tessdataDirectory));
            var sessionId = await _sessionStore.StartSessionAsync(DateTimeOffset.UtcNow);

            _activeSessionId = sessionId;
            _isEndingSession = false;
            _ocrInProgress = false;
            _catchRecognitionTask = null;
            _lastCompletedSessionId = null;
            ExportCsvButton.IsEnabled = false;
            ExportJsonButton.IsEnabled = false;
            _lastCatchFingerprint = null;
            _lastCatchAt = DateTimeOffset.MinValue;
            _lastOcrAt = DateTimeOffset.MinValue;

            EndSessionButton.IsEnabled = true;
            OcrStatusText.Text = "Local OCR ready. Images stay on this computer.";
            SessionStatusText.Text = "Session running. Start F7 observation to detect catch notifications.";
            SessionSummaryText.Text = string.Empty;
        }
        catch (Exception exception)
        {
            OcrStatusText.Text = $"Local OCR could not start: {exception.Message}";
            SessionStatusText.Text = "No session started. Check the network connection and the Microsoft Visual C++ 2022 runtime.";
            StartSessionButton.IsEnabled = true;
        }
    }

    private async void EndSession_Click(object sender, RoutedEventArgs e)
    {
        if (_automationEnabled)
        {
            StopAutomation("OCR session ended; Reel and Sell Anywhere inputs were stopped.");
        }

        if (_activeSessionId is not Guid sessionId)
        {
            return;
        }

        _isEndingSession = true;
        EndSessionButton.IsEnabled = false;
        SessionStatusText.Text = "Finishing pending OCR recognition and saving session…";

        try
        {
            if (_catchRecognitionTask is Task pendingRecognition)
            {
                await pendingRecognition;
            }

            await _sessionStore.FinishSessionAsync(sessionId, DateTimeOffset.UtcNow);
            _activeSessionId = null;
            _lastCompletedSessionId = sessionId;
            ExportCsvButton.IsEnabled = true;
            ExportJsonButton.IsEnabled = true;

            var summary = await _sessionStore.GetSessionSummaryAsync(sessionId);
            if (summary is null)
            {
                SessionStatusText.Text = "Session ended, but its summary could not be loaded.";
                return;
            }

            var topLines = summary.TopCatches
                .Select((fish, index) =>
                {
                    var rarity = fish.Rarity?.ToString() ?? "Unknown rarity";
                    var weight = fish.WeightKg.HasValue ? $" · {fish.WeightKg.Value:F2} kg" : string.Empty;
                    var mutation = !string.IsNullOrWhiteSpace(fish.Mutation) ? $" · {fish.Mutation}" : string.Empty;
                    return $"{index + 1}. {fish.Species ?? "Unidentified fish"} · {rarity}{mutation}{weight}";
                });

            SessionSummaryText.Text =
                $"TOP {summary.TopCatches.Count} FISH{Environment.NewLine}" +
                (summary.TopCatches.Count == 0 ? "No catches were identified." : string.Join(Environment.NewLine, topLines));

            SessionStatusText.Text =
                $"Session ended · {summary.TotalCatches} catches · {summary.IdentifiedCatches} identified · " +
                $"{summary.Duration.TotalMinutes:F1} min · {summary.CatchesPerHour:F1} catches/hour.";

            OcrStatusText.Text = "Local OCR remains ready for the next session.";
            StartSessionButton.IsEnabled = true;
            await RefreshLifetimeStatisticsAsync();
        }
        catch (Exception exception)
        {
            SessionStatusText.Text = $"Could not finish the session cleanly: {exception.Message}";
            StartSessionButton.IsEnabled = true;
        }
        finally
        {
            _isEndingSession = false;
        }
    }

    private void CopySessionSummary_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(SessionSummaryText.Text))
        {
            SessionStatusText.Text = "There is no completed session summary to copy.";
            return;
        }
        try
        {
            Clipboard.SetText(SessionSummaryText.Text);
            SessionStatusText.Text = "Session summary copied.";
        }
        catch (Exception exception) when (
            exception is System.Runtime.InteropServices.ExternalException or InvalidOperationException)
        {
            SessionStatusText.Text = $"Could not copy summary: {exception.Message}";
        }
    }

    private void ExportDiagnostics_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Title = "Export GafiFish diagnostics",
            Filter = "JSON diagnostics (*.json)|*.json",
            DefaultExt = "json",
            AddExtension = true,
            FileName = $"GafiFish-diagnostics-{DateTime.Now:yyyyMMdd-HHmmss}.json",
            OverwritePrompt = true
        };
        if (dialog.ShowDialog(this) != true)
            return;

        var report = new
        {
            GeneratedAtUtc = DateTimeOffset.UtcNow,
            Application = "GafiFish",
            Runtime = Environment.Version.ToString(),
            OperatingSystem = Environment.OSVersion.ToString(),
            Profile = _activeProfileName,
            Capture = new
            {
                IsObserving = _previewCancellation is not null,
                Width = _lastFrame?.Width,
                Height = _lastFrame?.Height,
                FramesPerSecond = _smoothedCaptureFps
            },
            Calibration = new
            {
                Fishing = _profile.IsFishingCalibrated,
                PerfectCast = _profile.IsCastCalibrated,
                Shake = _profile.IsShakeCalibrated,
                SellButton = _profile.IsSellButtonCalibrated,
                Inventory = _profile.IsInventoryVerificationCalibrated,
                CatchNotification = _profile.IsCatchNotificationCalibrated
            },
            Automation = new
            {
                Enabled = _automationEnabled,
                State = _runtimeStateMachine?.CurrentState.ToString() ?? "Disarmed",
                AutoCast = _autoCastEnabled,
                AutoShake = _autoShakeEnabled,
                AutoTotem = _autoTotemEnabled,
                AutoPotion = _autoPotionEnabled,
                TargetFish = _targetFishEnabled ? _targetFishName : null,
                PrioritySpecies = WatchSpeciesTextBox.Text,
                AlertLegendary = AlertLegendaryCheckBox.IsChecked == true,
                AlertMutation = AlertMutationCheckBox.IsChecked == true
            },
            Status = new
            {
                Vision = VisionStatusText.Text,
                Cast = CastStatusText.Text,
                Shake = ShakeStatusText.Text,
                Sell = SellStatusText.Text,
                Automation = AutomationStatusText.Text,
                OCR = OcrStatusText.Text,
                Session = SessionStatusText.Text,
                Telemetry = TelemetryText.Text
            }
        };

        try
        {
            File.WriteAllText(dialog.FileName,
                JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
            SessionStatusText.Text = $"Diagnostics exported to {System.IO.Path.GetFileName(dialog.FileName)}.";
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or JsonException)
        {
            SessionStatusText.Text = $"Diagnostics export failed: {exception.Message}";
        }
    }

    private async void ExportCsv_Click(object sender, RoutedEventArgs e)
    {
        await ExportCompletedSessionAsync(exportJson: false);
    }

    private async void ExportJson_Click(object sender, RoutedEventArgs e)
    {
        await ExportCompletedSessionAsync(exportJson: true);
    }

    private async Task ExportCompletedSessionAsync(bool exportJson)
    {
        if (_lastCompletedSessionId is not Guid sessionId)
        {
            SessionStatusText.Text = "End a session before exporting its catch history.";
            return;
        }

        var extension = exportJson ? "json" : "csv";
        var dialog = new SaveFileDialog
        {
            Title = exportJson ? "Export GafiFish session as JSON" : "Export GafiFish session as CSV",
            Filter = exportJson ? "JSON files (*.json)|*.json" : "CSV files (*.csv)|*.csv",
            DefaultExt = extension,
            AddExtension = true,
            FileName = $"GafiFish-session-{DateTime.Now:yyyyMMdd-HHmmss}.{extension}",
            OverwritePrompt = true
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        ExportCsvButton.IsEnabled = false;
        ExportJsonButton.IsEnabled = false;
        try
        {
            var catches = await _sessionStore.GetCatchesAsync(sessionId);
            if (exportJson)
            {
                await SessionExportService.ExportJsonAsync(catches, dialog.FileName);
            }
            else
            {
                await SessionExportService.ExportCsvAsync(catches, dialog.FileName);
            }

            SessionStatusText.Text = $"Exported {catches.Count} catches to {System.IO.Path.GetFileName(dialog.FileName)}.";
        }
        catch (Exception exception)
        {
            SessionStatusText.Text = $"Export failed: {exception.Message}";
        }
        finally
        {
            ExportCsvButton.IsEnabled = true;
            ExportJsonButton.IsEnabled = true;
        }
    }

    private void TryStartCatchRecognition(ScreenCaptureFrame frame)
    {
        if (_isEndingSession
            || _activeSessionId is not Guid sessionId
            || _ocr is null
            || !_profile.IsCatchNotificationCalibrated
            || _ocrInProgress
            || _catchRecognitionTask is not null
            || frame.CapturedAt - _lastOcrAt < TimeSpan.FromMilliseconds(900))
        {
            return;
        }

        _lastOcrAt = frame.CapturedAt;
        _ocrInProgress = true;

        var completion = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        _catchRecognitionTask = completion.Task;

        _ = ProcessCatchRecognitionTrackedAsync(
            frame,
            sessionId,
            _ocr,
            _profile.CatchNotificationRegion,
            completion);
    }

    private async Task ProcessCatchRecognitionTrackedAsync(
        ScreenCaptureFrame frame,
        Guid sessionId,
        LocalTesseractOcr ocr,
        NormalizedRegion region,
        TaskCompletionSource completion)
    {
        try
        {
            await ProcessCatchRecognitionAsync(frame, sessionId, ocr, region);
        }
        finally
        {
            if (ReferenceEquals(_catchRecognitionTask, completion.Task))
            {
                _catchRecognitionTask = null;
            }

            completion.TrySetResult();
        }
    }

    private async Task ProcessCatchRecognitionAsync(
        ScreenCaptureFrame frame,
        Guid sessionId,
        LocalTesseractOcr ocr,
        NormalizedRegion region)
    {
        try
        {
            var result = await Task.Run(() => ocr.Recognize(frame, region));

            if (_activeSessionId != sessionId)
            {
                return;
            }

            var displayText = string.Join(
                " ",
                result.Text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

            OcrStatusText.Text = result.HasText
                ? $"OCR {result.Confidence:P0}: {displayText[..Math.Min(displayText.Length, 110)]}"
                : "OCR: no text found in the selected notification region.";

            if (!_catchParser.TryParse(result, out var fishCatch, out var parseStatus)
                || fishCatch is null)
            {
                if (!string.IsNullOrWhiteSpace(parseStatus)
                    && parseStatus.StartsWith("A catch notification", StringComparison.OrdinalIgnoreCase))
                {
                    SessionStatusText.Text = parseStatus;
                }

                return;
            }

            var targetFishCaught = _automationEnabled
                && _targetFishEnabled
                && !string.IsNullOrWhiteSpace(_targetFishName)
                && FishCatchTargetMatcher.Matches(result.Text, _targetFishName)
                && result.Confidence >= 0.90
                && _runtimeStateMachine is { CurrentState: FishingState.Reeling };

            var stopAfterFirstCatch = _automationEnabled
                && _keepFishingUntilCatchEnabled
                && result.Confidence >= 0.90
                && _runtimeStateMachine is { CurrentState: FishingState.Reeling };

            if (targetFishCaught || stopAfterFirstCatch)
            {
                if (!_runtimeStateMachine!.TryTransition(
                        FishingState.CatchDetected,
                        targetFishCaught
                            ? $"A high-confidence local OCR notification matched target '{_targetFishName}'."
                            : "A high-confidence local OCR notification confirmed the first caught fish.",
                        frame.CapturedAt))
                {
                    StopAutomation("The catch was recognised but the fishing state could not confirm it; all inputs released.");
                    return;
                }

                // Stop before the database write so no later frame can start a new
                // cycle or trigger a periodic inventory action after the first catch.
                StopAutomation(targetFishCaught
                    ? $"Target fish confirmed ({_targetFishName}); target-fishing mode completed."
                    : $"First catch confirmed ({fishCatch.Species}); keep-fishing mode completed.");
            }

            var fingerprint = string.Join(
                "|",
                fishCatch.Species,
                fishCatch.Rarity?.ToString() ?? string.Empty,
                fishCatch.Mutation ?? string.Empty,
                fishCatch.WeightKg?.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty);

            if (string.Equals(fingerprint, _lastCatchFingerprint, StringComparison.OrdinalIgnoreCase)
                && frame.CapturedAt - _lastCatchAt < TimeSpan.FromSeconds(6))
            {
                return;
            }

            await _sessionStore.SaveCatchAsync(sessionId, fishCatch);
            AlertForCatchIfNeeded(fishCatch);
            await RefreshLifetimeStatisticsAsync();
            _lastCatchFingerprint = fingerprint;
            _lastCatchAt = frame.CapturedAt;

            if (_automationEnabled
                && _runtimeStateMachine is not null
                && result.Confidence >= 0.90
                && _runtimeStateMachine.CurrentState == FishingState.Reeling)
            {
                _ = _runtimeStateMachine.TryTransition(
                    FishingState.CatchDetected,
                    "A high-confidence OCR fish-catch notification was saved while Reel was active.",
                    frame.CapturedAt);
            }

            SessionStatusText.Text =
                $"Catch saved: {fishCatch.Species}" +
                (fishCatch.Rarity.HasValue ? $" · {fishCatch.Rarity.Value}" : string.Empty) +
                (fishCatch.WeightKg.HasValue ? $" · {fishCatch.WeightKg.Value:F2} kg" : string.Empty);

        }
        catch (Exception exception)
        {
            OcrStatusText.Text = $"OCR paused: {exception.Message}";
        }
        finally
        {
            _ocrInProgress = false;
        }
    }

    private async void Window_Closed(object? sender, EventArgs e)
    {
        StopAutomation("Application closing; all inputs released.");
        StopPreviewLoop();
        _isEndingSession = true;

        if (_catchRecognitionTask is Task pendingRecognition)
        {
            try
            {
                await pendingRecognition;
            }
            catch
            {
                // A failed OCR operation must not prevent input release or application shutdown.
            }
        }

        if (_activeSessionId is Guid sessionId)
        {
            try
            {
                await _sessionStore.FinishSessionAsync(sessionId, DateTimeOffset.UtcNow);
                _activeSessionId = null;
            }
            catch
            {
                // Preserve the existing session record if finalisation fails.
            }
        }

        _ocr?.Dispose();
        _ocr = null;

        var handle = new WindowInteropHelper(this).Handle;
        if (_captureHotkeyRegistered)
        {
            Native.UnregisterHotKey(handle, CaptureHotkeyId);
        }

        if (_previewHotkeyRegistered)
        {
            Native.UnregisterHotKey(handle, PreviewHotkeyId);
        }

        if (_reelArmHotkeyRegistered)
        {
            Native.UnregisterHotKey(handle, ReelArmHotkeyId);
        }

        if (_emergencyStopHotkeyRegistered)
        {
            Native.UnregisterHotKey(handle, EmergencyStopHotkeyId);
        }

        _source?.RemoveHook(WindowMessageHook);
        _source?.Dispose();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private static class Native
    {
        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool RegisterHotKey(IntPtr hWnd, int id, uint modifiers, uint virtualKey);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    }
}
