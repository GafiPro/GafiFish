using System.IO;
using System.IO.Compression;
using System.Diagnostics;
using System.Collections.ObjectModel;
using System.Text.Json;
using System.Threading;
using Microsoft.Win32;
using System.Runtime.InteropServices;
using Mat = OpenCvSharp.Mat;
using MatType = OpenCvSharp.MatType;
using Cv2 = OpenCvSharp.Cv2;
using ColorConversionCodes = OpenCvSharp.ColorConversionCodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using GafiFish.Vision.Calibration;
using GafiFish.Vision.Detection;
using GafiFish.Core.Casting;
using GafiFish.Vision.Tracking;
using GafiFish.Windows.Capture;
using GafiFish.Core.Models;
using GafiFish.Data.Storage;
using GafiFish.Data.Models;
using GafiFish.Data.Export;
using GafiFish.Ocr.Engine;
using GafiFish.Ocr.Installation;
using GafiFish.Ocr.Parsing;
using GafiFish.Core.Automation;
using GafiFish.Core.Aquarium;
using GafiFish.Core.Diagnostics;
using GafiFish.Core.Control;
using GafiFish.Core.Scheduling;
using GafiFish.Core.Services;
using GafiFish.App.Automation;
using GafiFish.App.Diagnostics;
using GafiFish.App.Settings;
using GafiFish.App.Logging;
using GafiFish.App.Backup;
using GafiFish.App.Models;
using GafiFish.Windows.Input;

namespace GafiFish.App;

public partial class MainWindow : Window
{
    private const int WmHotkey = 0x0312;
    private const int CaptureHotkeyId = 0x4711;
    private const int PreviewHotkeyId = 0x4712;
    private const int ReelArmHotkeyId = 0x4713;
    private const int EmergencyStopHotkeyId = 0x4714;
    private const int ToggleUiHotkeyId = 0x4715;
    private const uint ModNoRepeat = 0x4000;
    private static readonly string[] AvailableFunctionKeys =
        Enumerable.Range(2, 11).Select(number => $"F{number}").ToArray();

    private readonly RobloxWindowCapture _capture = new();
    private readonly FishingVisionDetector _vision = new();
    private readonly RodChargeMeterVisionDetector _rodChargeMeterVision = new();
    private readonly CastMeterVisionDetector _castVision = new();
    private readonly ShakePromptVisionDetector _shakeVision = new();
    private readonly SellAnywhereVisionDetector _sellVision = new();
    private readonly ConnectionRecoveryVisionDetector _connectionRecoveryVision = new();
    private readonly WorldPhaseVisionDetector _worldPhaseVision = new();
    private readonly HuntSignalVisionDetector _huntSignalVision = new();
    private readonly HuntDetectionCoordinator _huntDetectionCoordinator = new();
    private readonly PerfectCastController _perfectCast = new();
    private readonly FishMotionTracker _motionTracker = new();
    private VisionProfileStore _profileStore = new();
    private string _activeProfileName = "Default";
    private readonly FishSessionStore _sessionStore = new();
    private readonly FishCatchTextParser _catchParser = new();
    private readonly AppHotkeySettingsStore _hotkeySettingsStore = new();
    private readonly RodProfileBindingStore _rodProfileBindingStore = new();
    private RodProfileSlotBindings _rodProfileBindings = new();
    private bool _isLoadingBoundRodProfile;
    private bool _rodProfileBindingError;
    private readonly SemaphoreSlim _ocrInitializationGate = new(1, 1);
    private bool _isLoadingRodReelPreset = true;
    private DateTimeOffset _lastRodNameOcrAt = DateTimeOffset.MinValue;
    private int _rodNameOcrInProgress;
    private string? _pendingRodNameKey;
    private int _pendingRodNameFrames;
    private int _rodProfileSelectionVersion;
    private readonly CatchAlertSettingsStore _catchAlertSettingsStore = new();
    private readonly AquariumScheduleStore _aquariumScheduleStore = new();
    private AquariumScheduleState _aquariumScheduleState = new();
    private readonly AquariumReadyAlertTracker _aquariumReadyAlertTracker = new();
    private readonly AquariumButtonVisionDetector _aquariumButtonVision = new();
    private AquariumAutomationCoordinator? _aquariumAutomationCoordinator;
    private RobloxInputService? _aquariumAutomationInput;
    private bool _aquariumStartRequested;
    private ConnectionRecoveryCoordinator? _connectionRecoveryCoordinator;
    private RobloxInputService? _connectionRecoveryInput;
    private bool _connectionRecoveryStartRequested;
    private DayNightTotemRotationCoordinator? _dayNightTotemRotation;
    private AquariumActionKind _aquariumCalibrationActionKind = AquariumActionKind.FeedGuppiesBest;
    private readonly DispatcherTimer _aquariumTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly DispatcherTimer _frameReplayTimer = new() { Interval = TimeSpan.FromMilliseconds(120) };
    private FrameReplaySequence? _frameReplaySequence;
    private VideoReplaySession? _videoReplaySession;
    private string? _videoReplayFileName;
    private int _videoReplayFrameStep = 1;
    private bool _isLoadingAquariumSchedule;
    private readonly ObservableCollection<string> _activityLog = [];
    private readonly ObservableCollection<string> _visibleActivityLog = [];
    private readonly List<CatchHistoryRow> _allCatchHistoryRows = [];
    private readonly ObservableCollection<CatchHistoryRow> _visibleCatchHistoryRows = [];
    private readonly ActivityLogStore _activityLogStore = new();
    private readonly UserDataBackupService _userDataBackupService = new();
    private AppHotkeySettings _hotkeySettings = new();
    private bool _toggleUiHotkeyRegistered;
    private bool _isUiHidden;
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
    private bool _emergencyStopHotkeyUsesDispatcher;
    private EmergencyStopHotkeyListener? _emergencyStopHotkeyListener;
    private int _emergencyStopLatch;
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
    private DateTimeOffset? _targetFishStartedAt;
    private bool _startRequested;
    private MissedBiteRetryPolicy _missedBiteRetries = new();
    private bool _automationEnabled;
    private LocalTesseractOcr? _ocr;
    private Guid? _activeSessionId;
    private Guid? _lastCompletedSessionId;
    private bool _ocrInProgress;
    private Task? _catchRecognitionTask;
    private bool _isEndingSession;
    private bool _isRefreshingSessionHistory;
    private DateTimeOffset? _dashboardSessionStartedAt;
    private IReadOnlyList<FishCatch> _dashboardSessionCatches = Array.Empty<FishCatch>();
    private readonly DispatcherTimer _dashboardSessionTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private CatchGoalProgress? _targetCatchProgress;
    private readonly AnglerQuestTracker _anglerQuestTracker = new();
    private readonly AnglerQuestButtonVisionDetector _anglerQuestButtonVision = new();
    private AnglerQuestAutomationCoordinator? _anglerQuestAutomationCoordinator;
    private RobloxInputService? _anglerQuestAutomationInput;
    private bool _anglerQuestStartRequested;
    private string? _anglerQuestRequestedSpecies;
    private readonly AnglerQuestStateStore _anglerQuestStateStore = new();
    private bool _anglerQuestStateAvailable = true;
    private bool _anglerQuestAssistEnabled;
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
        SelectCatchNotificationRegion,
        SelectReconnectButtonRegion,
        SampleReconnectButtonColor,
        CaptureFishMarkerTemplate,
        CaptureControlMarkerTemplate,
        SelectWorldPhaseRegion,
        SampleDayPhaseColor,
        SampleNightPhaseColor,
        SelectHuntSignalRegion,
        SampleHuntSignalColor,
        SelectAquariumButtonRegion,
        SampleAquariumButtonColor,
        SelectAnglerAcceptButtonRegion,
        SampleAnglerAcceptButtonColor,
        SelectAnglerSubmitButtonRegion,
        SampleAnglerSubmitButtonColor,
        SelectRodNameRegion,
        SelectRodChargeMeterRegion,
        SampleRodChargeMeterColor
    }

    public MainWindow()
    {
        InitializeComponent();
        _dashboardSessionTimer.Tick += DashboardSessionTimer_Tick;
        _aquariumTimer.Tick += AquariumTimer_Tick;
        _frameReplayTimer.Tick += FrameReplayTimer_Tick;
        PreviewOverlay.Children.Add(_fishMarkerOverlay);
        PreviewOverlay.Children.Add(_controlMarkerOverlay);
        PreviewOverlay.Children.Add(_castTargetOverlay);
        PreviewOverlay.Children.Add(_castIndicatorOverlay);
        PreviewOverlay.Children.Add(_sellButtonOverlay);
        ActivityLogList.ItemsSource = _visibleActivityLog;
        ActivityLogLevelComboBox.ItemsSource = new[] { "All levels", "INFO", "WARN", "ERROR", "DEBUG" };
        ActivityLogLevelComboBox.SelectedIndex = 0;
        CatchHistoryGrid.ItemsSource = _visibleCatchHistoryRows;
        CatchRarityFilterComboBox.ItemsSource = new[]
        {
            "All rarities", "Unknown", "Common", "Unusual", "Rare",
            "Legendary", "Mythical", "Exotic", "Secret", "Apex"
        };
        CatchRarityFilterComboBox.SelectedIndex = 0;
        CapturePerformanceModeComboBox.SelectionChanged += CapturePerformanceMode_SelectionChanged;
        InitializeHotkeyControls();
        Loaded += Window_Loaded;
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        RodProfileNameTextBox.Text = _activeProfileName;
        _profile = await _profileStore.LoadOrDefaultAsync();
        RefreshRodReelPresetControls();
        RodNameRecognitionStatusText.Text = _profile.RodNameRegionCalibrated
            ? "Rod-name OCR region loaded. Press Recognize now once to initialise OCR, then enable automatic recognition."
            : "Select the rod-name label region in Calibration before enabling recognition.";
        _hotkeySettings = await _hotkeySettingsStore.LoadAsync();
        _rodProfileBindings = await _rodProfileBindingStore.LoadAsync();
        RefreshRodProfileBindingStatus();
        try
        {
            var anglerSnapshot = await _anglerQuestStateStore.LoadAsync();
            _anglerQuestTracker.Restore(anglerSnapshot);
            AnglerQuestStatusText.Text = _anglerQuestTracker.GetStatus(DateTimeOffset.UtcNow);
            AnglerQuestSubmittedButton.IsEnabled = _anglerQuestTracker.HasCatchToSubmit;
            if (!string.IsNullOrWhiteSpace(_anglerQuestTracker.TargetSpecies))
            {
                TargetFishTextBox.Text = _anglerQuestTracker.TargetSpecies;
                AnglerQuestAssistCheckBox.IsChecked = true;
            }
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or JsonException or ArgumentException)
        {
            _anglerQuestStateAvailable = false;
            AnglerQuestStatusText.Text =
                "Saved Angler quest state could not be read. Angler automation is disabled until the state file is repaired or removed. " + exception.Message;
            AddActivityLog("ERROR", "ANGLER", $"Saved quest state could not be loaded: {exception.Message}");
        }
        _aquariumScheduleState = await _aquariumScheduleStore.LoadAsync();
        _isLoadingAquariumSchedule = true;
        AquariumClaimReminderCheckBox.IsChecked = _aquariumScheduleState.ClaimReminderEnabled;
        AquariumReadySoundCheckBox.IsChecked = _aquariumScheduleState.ReadySoundEnabled;
        AquariumAutoFeedCheckBox.IsChecked = _aquariumScheduleState.AutoFeedEnabled;
        AquariumAutoRestockCheckBox.IsChecked = _aquariumScheduleState.AutoRestockEnabled;
        AquariumAutoClaimCheckBox.IsChecked = _aquariumScheduleState.AutoClaimEnabled;
        AquariumAutoFeedCheckBox.IsChecked = _aquariumScheduleState.AutoFeedEnabled;
        AquariumAutoRestockCheckBox.IsChecked = _aquariumScheduleState.AutoRestockEnabled;
        AquariumAutoClaimCheckBox.IsChecked = _aquariumScheduleState.AutoClaimEnabled;
        _isLoadingAquariumSchedule = false;
        RefreshAquariumPlannerDisplay(DateTimeOffset.UtcNow);
        _aquariumTimer.Start();
        var catchAlertSettings = await _catchAlertSettingsStore.LoadAsync();
        WatchSpeciesTextBox.Text = catchAlertSettings.WatchedSpecies;
        AlertLegendaryCheckBox.IsChecked = catchAlertSettings.AlertOnLegendaryOrHigher;
        AlertMutationCheckBox.IsChecked = catchAlertSettings.AlertOnMutation;
        MinimumAlertWeightTextBox.Text = catchAlertSettings.MinimumWeightKg?.ToString("0.##", System.Globalization.CultureInfo.CurrentCulture) ?? string.Empty;
        MinimumAlertValueTextBox.Text = catchAlertSettings.MinimumEstimatedValue?.ToString("0.##", System.Globalization.CultureInfo.CurrentCulture) ?? string.Empty;
        CatchAlertSettingsStatusText.Text = "Saved alert preferences loaded from this computer.";
        ApplyHotkeySettingsToControls();
        RegisterGlobalHotkeys();
        try
        {
            foreach (var entry in _activityLogStore.LoadRecent(500))
                _activityLog.Insert(0, entry);
            RefreshActivityLogView();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            ActivityLogStatusText.Text = $"Could not load saved activity log: {exception.Message}";
        }
        AddActivityLog("INFO", "SYSTEM", "GafiFish started. Local settings and calibration were loaded.");
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
        ConnectionRecoveryStatusText.Text = _profile.IsReconnectButtonCalibrated
            ? "Reconnect button calibration loaded. Watchdog ready."
            : "Calibrate the reconnect button region and colour before arming the watchdog.";
        WorldPhaseCalibrationStatusText.Text = _profile.IsWorldPhaseCalibrated
            ? "Day/night phase calibration loaded."
            : "Calibrate the phase region and sample the day and night colours.";
        DayNightTotemStatusText.Text = _profile.IsWorldPhaseCalibrated
            ? "Phase calibration loaded. The first stable phase will set a baseline."
            : "Day/night totems need phase calibration.";
        HuntDetectionStatusText.Text = _profile.IsHuntSignalCalibrated
            ? "Hunt cue calibration loaded. Enable alerts to monitor live frames."
            : "Select the hunt cue region and sample its colour in a visible hunt frame.";
        OcrStatusText.Text = _profile.IsCatchNotificationCalibrated
            ? "Local OCR: catch notification region loaded; start a session to initialise the engine."
            : "Local OCR: select the catch notification region before starting a session.";
        TelemetryText.Text = $"Profile: {_activeProfileName} · waiting for the first frame.";
        UpdateTimedItemControls();
        CapturePerformanceMode_SelectionChanged(CapturePerformanceModeComboBox, null!);
        RefreshModuleQuickInfo();
        UpdateConnectionRecoveryControls();
        await RefreshLifetimeStatisticsAsync();
        await RefreshSessionHistoryAsync();
    }

    private CapturePerformanceMode GetSelectedCapturePerformanceMode() =>
        CapturePerformanceModeComboBox.SelectedIndex switch
        {
            0 => CapturePerformanceMode.Economy,
            2 => CapturePerformanceMode.Responsive,
            _ => CapturePerformanceMode.Balanced
        };

    private void CapturePerformanceMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var mode = GetSelectedCapturePerformanceMode();
        PerformanceModeStatusText.Text = mode switch
        {
            CapturePerformanceMode.Economy => "Economy · lower CPU use · slower visual refresh.",
            CapturePerformanceMode.Responsive => "Responsive · more frequent capture · higher CPU use.",
            _ => "Balanced · adaptive capture cadence · recommended starting point."
        };
    }

    private async Task RefreshSessionHistoryAsync(Guid? preferredSessionId = null)
    {
        try
        {
            var previousId = preferredSessionId
                ?? (SessionHistoryList.SelectedItem as FishingSessionRecord)?.Id;
            var sessions = await _sessionStore.GetRecentSessionsAsync(20);
            var selectedSession = previousId.HasValue
                ? sessions.FirstOrDefault(session => session.Id == previousId.Value)
                : null;
            selectedSession ??= sessions.FirstOrDefault();

            _isRefreshingSessionHistory = true;
            try
            {
                SessionHistoryList.ItemsSource = sessions;
                SessionHistoryList.SelectedItem = selectedSession;
            }
            finally
            {
                _isRefreshingSessionHistory = false;
            }

            UpdateSessionExportButtons(selectedSession);
            if (selectedSession is null)
            {
                SelectedSessionDetailsText.Text = "No saved fishing sessions yet.";
                return;
            }

            await ShowSessionDetailsAsync(selectedSession);
        }
        catch (Exception exception)
        {
            SelectedSessionDetailsText.Text = $"Could not load session history: {exception.Message}";
            UpdateSessionExportButtons(null);
        }
    }

    private async Task ShowSessionDetailsAsync(FishingSessionRecord session)
    {
        try
        {
            var catches = await _sessionStore.GetCatchesAsync(session.Id);
            var finishedAt = session.EndedAt;
            var durationEnd = finishedAt ?? DateTimeOffset.UtcNow;
            var duration = durationEnd >= session.StartedAt
                ? durationEnd - session.StartedAt
                : TimeSpan.Zero;
            var status = finishedAt.HasValue
                ? $"Ended: {finishedAt.Value.ToLocalTime():dd/MM/yyyy HH:mm}"
                : "Status: In progress";

            var latestCatchLines = catches
                .OrderByDescending(fish => fish.CaughtAt)
                .Take(15)
                .Select(fish =>
                {
                    var species = string.IsNullOrWhiteSpace(fish.Species) ? "Unidentified fish" : fish.Species;
                    var rarity = fish.Rarity?.ToString() ?? "Unknown rarity";
                    var mutation = string.IsNullOrWhiteSpace(fish.Mutation) ? string.Empty : $" · {fish.Mutation}";
                    var weight = fish.WeightKg.HasValue ? $" · {fish.WeightKg.Value:F2} kg" : string.Empty;
                    var value = fish.EstimatedValue.HasValue ? $" · ~{fish.EstimatedValue.Value:N0}" : string.Empty;
                    return $"{fish.CaughtAt.ToLocalTime():HH:mm:ss} · {species} · {rarity}{mutation}{weight}{value}";
                });

            SelectedSessionDetailsText.Text =
                $"Started: {session.StartedAt.ToLocalTime():dd/MM/yyyy HH:mm}{Environment.NewLine}" +
                $"{status}{Environment.NewLine}" +
                $"Duration: {duration.TotalMinutes:F1} min · {catches.Count} catches{Environment.NewLine}" +
                (catches.Count > 15 ? "Latest 15 catches:" : "Catches:") + Environment.NewLine +
                (catches.Count == 0 ? "No catches saved in this session." : string.Join(Environment.NewLine, latestCatchLines));
        }
        catch (Exception exception)
        {
            SelectedSessionDetailsText.Text = $"Could not load this session: {exception.Message}";
        }
    }

    private void UpdateSessionExportButtons(FishingSessionRecord? selectedSession)
    {
        var canExport = selectedSession?.EndedAt is not null
            || (selectedSession is null && _lastCompletedSessionId.HasValue);
        ExportCsvButton.IsEnabled = canExport;
        ExportJsonButton.IsEnabled = canExport;
    }

    private async void RefreshSessionHistory_Click(object sender, RoutedEventArgs e)
    {
        await RefreshSessionHistoryAsync();
    }

    private async void SessionHistoryList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isRefreshingSessionHistory)
            return;

        var selectedSession = SessionHistoryList.SelectedItem as FishingSessionRecord;
        UpdateSessionExportButtons(selectedSession);
        if (selectedSession is null)
        {
            SelectedSessionDetailsText.Text = "Select a saved session to inspect its catches.";
            return;
        }

        await ShowSessionDetailsAsync(selectedSession);
    }

    private bool IsCatchbookSelected() =>
        MainTabs.SelectedItem is TabItem selectedTab
        && string.Equals(selectedTab.Header?.ToString(), "Catchbook", StringComparison.Ordinal);

    private async void MainTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!ReferenceEquals(e.OriginalSource, MainTabs))
            return;

        if (IsCatchbookSelected())
            await RefreshCatchArchiveAsync();
    }

    private async Task RefreshCatchArchiveAsync()
    {
        try
        {
            CatchArchiveStatusText.Text = "Loading the local catch archive…";
            var history = await _sessionStore.GetAllCatchHistoryAsync();

            _allCatchHistoryRows.Clear();
            foreach (var item in history)
            {
                var fish = item.Catch;
                _allCatchHistoryRows.Add(new CatchHistoryRow(
                    item.SessionId,
                    item.SessionStartedAt,
                    fish.CaughtAt,
                    fish.Species,
                    fish.Rarity,
                    fish.Mutation,
                    fish.WeightKg,
                    fish.EstimatedValue,
                    fish.RecognitionConfidence));
            }

            ApplyCatchHistoryFilters();
            CatchArchiveStatusText.Text =
                $"Loaded {_allCatchHistoryRows.Count:N0} catches from the local database.";
        }
        catch (Exception exception)
        {
            CatchArchiveStatusText.Text = $"Could not load catch archive: {exception.Message}";
            CatchArchiveSummaryText.Text = "Catch archive unavailable.";
            _visibleCatchHistoryRows.Clear();
            UpdateCatchArchiveActions();
        }
    }

    private void ApplyCatchHistoryFilters()
    {
        var filtered = CatchHistoryFilter.Apply(
            _allCatchHistoryRows,
            CatchHistorySearchTextBox.Text,
            CatchRarityFilterComboBox.SelectedItem as string ?? "All rarities",
            IdentifiedCatchesOnlyCheckBox.IsChecked == true,
            MutatedCatchesOnlyCheckBox.IsChecked == true);

        _visibleCatchHistoryRows.Clear();
        foreach (var item in filtered)
            _visibleCatchHistoryRows.Add(item);

        var identified = _allCatchHistoryRows.Count(item => item.IsIdentified);
        var mutated = _allCatchHistoryRows.Count(item => item.HasMutation);
        CatchArchiveSummaryText.Text =
            $"{_allCatchHistoryRows.Count:N0} total · {identified:N0} identified · {mutated:N0} with mutations";
        CatchArchiveStatusText.Text =
            $"Showing {_visibleCatchHistoryRows.Count:N0} of {_allCatchHistoryRows.Count:N0} catches.";
        UpdateCatchArchiveActions();
    }

    private void UpdateCatchArchiveActions()
    {
        if (ExportFilteredCatchHistoryButton is not null)
            ExportFilteredCatchHistoryButton.IsEnabled = _visibleCatchHistoryRows.Count > 0;
        if (ExportFilteredCatchHistoryJsonButton is not null)
            ExportFilteredCatchHistoryJsonButton.IsEnabled = _visibleCatchHistoryRows.Count > 0;
        if (CopySelectedCatchButton is not null)
            CopySelectedCatchButton.IsEnabled = CatchHistoryGrid.SelectedItem is CatchHistoryRow;
    }

    private void CatchHistorySearch_TextChanged(object sender, TextChangedEventArgs e) =>
        ApplyCatchHistoryFilters();

    private void CatchHistoryFilter_Changed(object sender, RoutedEventArgs e) =>
        ApplyCatchHistoryFilters();

    private void CatchHistoryRarity_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (IsLoaded)
            ApplyCatchHistoryFilters();
    }

    private void CatchHistoryGrid_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        UpdateCatchArchiveActions();

    private async void RefreshCatchArchive_Click(object sender, RoutedEventArgs e) =>
        await RefreshCatchArchiveAsync();

    private void ClearCatchHistoryFilters_Click(object sender, RoutedEventArgs e)
    {
        CatchHistorySearchTextBox.Clear();
        CatchRarityFilterComboBox.SelectedIndex = 0;
        IdentifiedCatchesOnlyCheckBox.IsChecked = false;
        MutatedCatchesOnlyCheckBox.IsChecked = false;
        ApplyCatchHistoryFilters();
    }

    private void CopySelectedCatch_Click(object sender, RoutedEventArgs e)
    {
        if (CatchHistoryGrid.SelectedItem is not CatchHistoryRow selected)
            return;

        var text = string.Join(Environment.NewLine,
            $"Species: {selected.SpeciesDisplay}",
            $"Rarity: {selected.RarityDisplay}",
            $"Mutation: {selected.MutationDisplay}",
            $"Caught: {selected.CaughtAtDisplay}",
            $"Weight: {selected.WeightDisplay}",
            $"Estimated value: {selected.EstimatedValueDisplay}",
            $"OCR confidence: {selected.ConfidenceDisplay}",
            $"Session: {selected.SessionStartedAtDisplay}",
            $"Session ID: {selected.SessionId:D}");

        try
        {
            Clipboard.SetText(text);
            CatchArchiveStatusText.Text = $"Copied {selected.SpeciesDisplay} catch details.";
        }
        catch (Exception exception) when (exception is System.Runtime.InteropServices.ExternalException or InvalidOperationException)
        {
            CatchArchiveStatusText.Text = $"Could not copy selected catch: {exception.Message}";
        }
    }

    private void ExportFilteredCatchHistoryCsv_Click(object sender, RoutedEventArgs e)
    {
        var rows = _visibleCatchHistoryRows.ToArray();
        if (rows.Length == 0)
            return;

        var dialog = new SaveFileDialog
        {
            Title = "Export filtered GafiFish catch archive",
            Filter = "CSV file (*.csv)|*.csv",
            DefaultExt = "csv",
            AddExtension = true,
            FileName = $"GafiFish-filtered-catches-{DateTime.Now:yyyyMMdd-HHmmss}.csv",
            OverwritePrompt = true
        };
        if (dialog.ShowDialog(this) != true)
            return;

        try
        {
            var builder = new System.Text.StringBuilder();
            builder.AppendLine("CaughtAt,Species,Rarity,Mutation,WeightKg,EstimatedValue,RecognitionConfidence,SessionStartedAt,SessionId");
            foreach (var item in rows)
            {
                var fields = new[]
                {
                    item.CaughtAt.ToLocalTime().ToString("O"),
                    item.Species ?? string.Empty,
                    item.Rarity?.ToString() ?? "Unknown",
                    item.Mutation ?? string.Empty,
                    item.WeightKg?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty,
                    item.EstimatedValue?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty,
                    item.RecognitionConfidence.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    item.SessionStartedAt.ToLocalTime().ToString("O"),
                    item.SessionId.ToString("D")
                };
                builder.AppendLine(string.Join(",", fields.Select(EscapeCsvCell)));
            }

            File.WriteAllText(dialog.FileName, builder.ToString(), new System.Text.UTF8Encoding(false));
            CatchArchiveStatusText.Text =
                $"Exported {rows.Length:N0} filtered catches to {System.IO.Path.GetFileName(dialog.FileName)}.";
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            CatchArchiveStatusText.Text = $"Could not export filtered catch archive: {exception.Message}";
        }
    }

    private void ExportFilteredCatchHistoryJson_Click(object sender, RoutedEventArgs e)
    {
        var rows = _visibleCatchHistoryRows.ToArray();
        if (rows.Length == 0)
            return;

        var dialog = new SaveFileDialog
        {
            Title = "Export filtered GafiFish catch archive as JSON",
            Filter = "JSON file (*.json)|*.json",
            DefaultExt = "json",
            AddExtension = true,
            FileName = $"GafiFish-filtered-catches-{DateTime.Now:yyyyMMdd-HHmmss}.json",
            OverwritePrompt = true
        };
        if (dialog.ShowDialog(this) != true)
            return;

        try
        {
            var records = rows.Select(item => new
            {
                item.CaughtAt,
                item.Species,
                item.Rarity,
                item.Mutation,
                item.WeightKg,
                item.EstimatedValue,
                item.RecognitionConfidence,
                item.SessionStartedAt,
                item.SessionId
            }).ToArray();
            var json = JsonSerializer.Serialize(records, new JsonSerializerOptions
            {
                WriteIndented = true,
                Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
            });
            File.WriteAllText(dialog.FileName, json);
            CatchArchiveStatusText.Text =
                $"Exported {rows.Length:N0} filtered catches to {System.IO.Path.GetFileName(dialog.FileName)}.";
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            CatchArchiveStatusText.Text = $"Could not export filtered catch archive: {exception.Message}";
        }
    }

    private static string EscapeCsvCell(string value) =>
        "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";

    private async Task RefreshDashboardSessionAsync()
    {
        try
        {
            if (_activeSessionId is not Guid sessionId)
            {
                _dashboardSessionTimer.Stop();
                _dashboardSessionStartedAt = null;
                _dashboardSessionCatches = Array.Empty<FishCatch>();
                DashboardSessionStatusText.Text =
                    "No active OCR session. Start one in Fishing to monitor live results.";
                DashboardSessionDurationText.Text = "—";
                DashboardSessionCatchesText.Text = "—";
                DashboardSessionRateText.Text = "—";
                DashboardSessionValueText.Text = "—";
                DashboardRecentCatchesText.Text = "Recent recognised catches will appear here.";
                RenderDashboardCatchActivity(DateTimeOffset.UtcNow);
                return;
            }

            var session = await _sessionStore.GetSessionAsync(sessionId);
            if (session is null)
            {
                DashboardSessionStatusText.Text = "The active session record could not be found.";
                return;
            }

            _dashboardSessionStartedAt = session.StartedAt;
            _dashboardSessionCatches = await _sessionStore.GetCatchesAsync(sessionId);
            _dashboardSessionTimer.Start();
            UpdateDashboardSessionDisplay(DateTimeOffset.UtcNow);
            RenderDashboardCatchActivity(DateTimeOffset.UtcNow);
        }
        catch (Exception exception)
        {
            DashboardSessionStatusText.Text = $"Could not refresh live session data: {exception.Message}";
        }
    }

    private void DashboardSessionTimer_Tick(object? sender, EventArgs e)
    {
        if (_dashboardSessionStartedAt.HasValue)
            UpdateDashboardSessionDisplay(DateTimeOffset.UtcNow);
    }

    private void UpdateDashboardSessionDisplay(DateTimeOffset now)
    {
        if (_dashboardSessionStartedAt is not DateTimeOffset startedAt)
            return;

        var duration = now >= startedAt ? now - startedAt : TimeSpan.Zero;
        var catches = _dashboardSessionCatches.Count;
        var catchRate = duration.TotalHours > 0
            ? catches / duration.TotalHours
            : 0d;
        var estimatedValue = _dashboardSessionCatches
            .Where(fish => fish.EstimatedValue.HasValue)
            .Sum(fish => fish.EstimatedValue!.Value);

        var durationText = duration.TotalHours >= 1
            ? $"{(int)duration.TotalHours}h {duration.Minutes:00}m"
            : $"{duration.Minutes}m {duration.Seconds:00}s";

        DashboardSessionStatusText.Text =
            $"SESSION ACTIVE · started {startedAt.ToLocalTime():HH:mm}";
        DashboardSessionDurationText.Text = durationText;
        DashboardSessionCatchesText.Text = catches.ToString("N0");
        DashboardSessionRateText.Text = $"{catchRate:F1}/h";
        DashboardSessionValueText.Text = $"~{estimatedValue:N0}";

        var latestCatches = _dashboardSessionCatches
            .OrderByDescending(fish => fish.CaughtAt)
            .Take(5)
            .Select(fish =>
            {
                var species = string.IsNullOrWhiteSpace(fish.Species)
                    ? "Unidentified fish"
                    : fish.Species;
                var rarity = fish.Rarity?.ToString() ?? "Unknown";
                var mutation = string.IsNullOrWhiteSpace(fish.Mutation)
                    ? string.Empty
                    : $" · {fish.Mutation}";
                var weight = fish.WeightKg.HasValue
                    ? $" · {fish.WeightKg.Value:F2} kg"
                    : string.Empty;
                return $"{fish.CaughtAt.ToLocalTime():HH:mm:ss} · {species} · {rarity}{mutation}{weight}";
            });

        DashboardRecentCatchesText.Text = catches == 0
            ? "Waiting for the first recognised catch."
            : string.Join(Environment.NewLine, latestCatches);
    }

    private void RenderDashboardCatchActivity(DateTimeOffset now)
    {
        if (DashboardActivityChart is null)
            return;

        DashboardActivityChart.Children.Clear();
        const int binCount = 6;
        var counts = new int[binCount];

        foreach (var fish in _dashboardSessionCatches)
        {
            var age = now - fish.CaughtAt;
            if (age < TimeSpan.Zero || age >= TimeSpan.FromMinutes(30))
                continue;

            var index = binCount - 1 - Math.Min(binCount - 1, (int)(age.TotalMinutes / 5));
            counts[index]++;
        }

        var width = DashboardActivityChart.ActualWidth;
        if (width <= 1)
            return;

        const double gap = 8;
        const double chartBottom = 52;
        const double maxBarHeight = 38;
        var barWidth = (width - gap * (binCount - 1)) / binCount;
        if (barWidth <= 2)
            return;

        var maxCount = Math.Max(1, counts.Max());
        var bucketLabels = new[] { "−30m", "−25m", "−20m", "−15m", "−10m", "NOW" };
        for (var index = 0; index < binCount; index++)
        {
            var left = index * (barWidth + gap);
            var barHeight = counts[index] == 0
                ? 2
                : Math.Max(5, counts[index] / (double)maxCount * maxBarHeight);

            var bar = new Rectangle
            {
                Width = barWidth,
                Height = barHeight,
                RadiusX = 4,
                RadiusY = 4,
                Fill = new SolidColorBrush(Color.FromRgb(103, 213, 181))
            };
            Canvas.SetLeft(bar, left);
            Canvas.SetTop(bar, chartBottom - barHeight);
            DashboardActivityChart.Children.Add(bar);

            if (counts[index] > 0)
            {
                var countLabel = new TextBlock
                {
                    Text = counts[index].ToString(),
                    Width = barWidth,
                    FontSize = 9,
                    Foreground = Brushes.White,
                    TextAlignment = TextAlignment.Center
                };
                Canvas.SetLeft(countLabel, left);
                Canvas.SetTop(countLabel, Math.Max(0, chartBottom - barHeight - 14));
                DashboardActivityChart.Children.Add(countLabel);
            }

            var timeLabel = new TextBlock
            {
                Text = bucketLabels[index],
                Width = barWidth,
                FontSize = 8,
                Foreground = new SolidColorBrush(Color.FromRgb(142, 164, 181)),
                TextAlignment = TextAlignment.Center
            };
            Canvas.SetLeft(timeLabel, left);
            Canvas.SetTop(timeLabel, 60);
            DashboardActivityChart.Children.Add(timeLabel);
        }
    }

    private void DashboardActivityChart_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        RenderDashboardCatchActivity(DateTimeOffset.UtcNow);
    }

    private void AquariumTimer_Tick(object? sender, EventArgs e)
    {
        var now = DateTimeOffset.UtcNow;
        RefreshAquariumPlannerDisplay(now);
        if (_anglerQuestTracker.HasCatchToSubmit || _anglerQuestTracker.IsInCooldown)
            AnglerQuestStatusText.Text = _anglerQuestTracker.GetStatus(now);

        if (!_aquariumScheduleState.ReadySoundEnabled)
            return;

        var readyActions = _aquariumReadyAlertTracker.Evaluate(_aquariumScheduleState, now);
        if (readyActions.Count == 0)
            return;

        try
        {
            System.Media.SystemSounds.Asterisk.Play();
        }
        catch (Exception exception)
        {
            AddActivityLog("WARN", "AQUARIUM", $"Ready sound could not be played: {exception.Message}");
        }

        var summary = string.Join(", ", readyActions);
        AquariumPlannerStatusText.Text = $"Ready now: {summary}";
        AddActivityLog("INFO", "AQUARIUM", $"Cooldown ready: {summary}");
    }


    private AquariumAutomationOptions ReadAquariumAutomationOptions() =>
        new(
            AquariumAutoFeedCheckBox.IsChecked == true,
            AquariumAutoRestockCheckBox.IsChecked == true,
            AquariumAutoClaimCheckBox.IsChecked == true);

    private static IReadOnlyList<AquariumActionKind> GetAquariumActions(AquariumAutomationOptions options)
    {
        var actions = new List<AquariumActionKind>();
        foreach (var action in Enum.GetValues<AquariumActionKind>())
        {
            var enabled = action switch
            {
                AquariumActionKind.FeedGuppiesBest or AquariumActionKind.FeedFishChow
                    or AquariumActionKind.FeedSplashSnacks => options.FeedEnabled,
                AquariumActionKind.RestockGuppiesBest or AquariumActionKind.RestockFishChow
                    or AquariumActionKind.RestockSplashSnacks => options.RestockEnabled,
                AquariumActionKind.ClaimReward => options.ClaimEnabled,
                _ => false
            };
            if (enabled)
                actions.Add(action);
        }
        return actions;
    }

    private AquariumActionKind ReadAquariumCalibrationAction()
    {
        if (AquariumVisualActionComboBox.SelectedItem is ComboBoxItem item
            && item.Tag is string tag
            && Enum.TryParse<AquariumActionKind>(tag, ignoreCase: true, out var action))
            return action;

        throw new InvalidOperationException("Select a valid Aquarium action to calibrate.");
    }

    private void SelectAnglerAcceptButtonRegion_Click(object sender, RoutedEventArgs e) =>
        SetCalibrationAction(
            CalibrationAction.SelectAnglerAcceptButtonRegion,
            "Drag a tight rectangle around the enabled Angler Accept Request button.");

    private void SampleAnglerAcceptButtonColor_Click(object sender, RoutedEventArgs e) =>
        SetCalibrationAction(
            CalibrationAction.SampleAnglerAcceptButtonColor,
            "Click a clear pixel inside the enabled Angler Accept Request button.");

    private void SelectAnglerSubmitButtonRegion_Click(object sender, RoutedEventArgs e) =>
        SetCalibrationAction(
            CalibrationAction.SelectAnglerSubmitButtonRegion,
            "Drag a tight rectangle around the enabled Angler Submit Fish button.");

    private void SampleAnglerSubmitButtonColor_Click(object sender, RoutedEventArgs e) =>
        SetCalibrationAction(
            CalibrationAction.SampleAnglerSubmitButtonColor,
            "Click a clear pixel inside the enabled Angler Submit Fish button.");

    private void StartAnglerQuestAutomation_Click(object sender, RoutedEventArgs e)
    {
        if (!_anglerQuestStateAvailable)
        {
            AnglerQuestAutomationStatusText.Text =
                "Angler automation is unavailable because the saved quest state could not be read or written.";
            return;
        }

        if (_anglerQuestAutomationCoordinator?.IsStarted == true || _anglerQuestStartRequested)
        {
            AnglerQuestAutomationStatusText.Text = "Angler automation is already armed or waiting for Roblox focus.";
            return;
        }

        if (_automationEnabled || _startRequested || _aquariumStartRequested || _aquariumAutomationCoordinator?.IsStarted == true
            || _connectionRecoveryStartRequested || _connectionRecoveryCoordinator?.IsStarted == true)
        {
            AnglerQuestAutomationStatusText.Text =
                "Stop the fishing macro, Auto Aquarium and reconnect watchdog before arming Angler interactions.";
            return;
        }

        if (AnglerQuestAssistCheckBox.IsChecked != true)
        {
            AnglerQuestAutomationStatusText.Text =
                "Enable Angler Quest assist first and enter the exact requested fish name above.";
            return;
        }

        var now = DateTimeOffset.UtcNow;
        if (_anglerQuestTracker.HasCatchToSubmit)
        {
            if (!_profile.IsAnglerSubmitButtonCalibrated)
            {
                AnglerQuestAutomationStatusText.Text =
                    "Calibrate the Submit Fish button region and colour before arming hand-in automation.";
                return;
            }
        }
        else if (_anglerQuestTracker.IsTracking)
        {
            AnglerQuestAutomationStatusText.Text =
                "The Angler request is already accepted. Start the fishing macro to catch the requested fish.";
            return;
        }
        else
        {
            if (!_anglerQuestTracker.CanStartNewQuest(now))
            {
                AnglerQuestAutomationStatusText.Text = _anglerQuestTracker.GetStatus(now);
                return;
            }

            if (string.IsNullOrWhiteSpace(TargetFishTextBox.Text))
            {
                AnglerQuestAutomationStatusText.Text = "Enter the requested fish name before arming Angler automation.";
                return;
            }

            if (!_profile.IsAnglerAcceptButtonCalibrated)
            {
                AnglerQuestAutomationStatusText.Text =
                    "Calibrate the Accept Request button region and colour before arming quest acceptance.";
                return;
            }
        }

        _anglerQuestRequestedSpecies = _anglerQuestTracker.HasCatchToSubmit
            ? _anglerQuestTracker.TargetSpecies
            : TargetFishTextBox.Text.Trim();

        if (_previewCancellation is null)
            TogglePreviewLoop();

        _anglerQuestStartRequested = true;
        StartAnglerQuestAutomationButton.IsEnabled = false;
        StopAnglerQuestAutomationButton.IsEnabled = true;
        AnglerQuestAutomationStatusText.Text =
            "Start requested. Switch to Roblox; the input guard will arm only after it captures the foreground game window.";
    }

    private void StartAnglerQuestAutomationNow(ScreenCaptureFrame frame)
    {
        if (!_anglerQuestStartRequested || Volatile.Read(ref _emergencyStopLatch) != 0)
            return;

        try
        {
            var foregroundProcessId = _capture.GetForegroundRobloxProcessId();
            if (!foregroundProcessId.HasValue || foregroundProcessId.Value != frame.ProcessId)
                throw new InvalidOperationException("Roblox lost foreground focus before Angler automation could arm.");

            var needsAccept = !_anglerQuestTracker.IsTracking && !_anglerQuestTracker.HasCatchToSubmit;
            if (needsAccept && !_profile.IsAnglerAcceptButtonCalibrated)
                throw new InvalidOperationException("Accept Request button calibration is missing.");
            if (_anglerQuestTracker.HasCatchToSubmit && !_profile.IsAnglerSubmitButtonCalibrated)
                throw new InvalidOperationException("Submit Fish button calibration is missing.");

            var input = new RobloxInputService();
            _anglerQuestAutomationInput = input;
            var coordinator = new AnglerQuestAutomationCoordinator(
                input,
                _anglerQuestButtonVision,
                AnglerQuestActionConfirmed);
            coordinator.Start();
            _anglerQuestAutomationCoordinator = coordinator;
            _anglerQuestStartRequested = false;
            AnglerQuestAutomationStatusText.Text = coordinator.LastStatus;
            AddActivityLog("INFO", "ANGLER", "Angler visual interaction armed.");
        }
        catch (Exception exception)
        {
            _anglerQuestStartRequested = false;
            _anglerQuestAutomationInput?.Dispose();
            _anglerQuestAutomationInput = null;
            _anglerQuestAutomationCoordinator = null;
            StartAnglerQuestAutomationButton.IsEnabled = true;
            StopAnglerQuestAutomationButton.IsEnabled = false;
            AnglerQuestAutomationStatusText.Text = $"Angler automation could not start: {exception.Message}";
            AddActivityLog("WARN", "ANGLER", $"Angler automation start refused: {exception.Message}");
        }
    }

    private void AnglerQuestActionConfirmed(AnglerQuestButtonKind action, DateTimeOffset confirmedAt)
    {
        try
        {
            if (action == AnglerQuestButtonKind.AcceptRequest)
            {
                var target = _anglerQuestRequestedSpecies ?? TargetFishTextBox.Text.Trim();
                _anglerQuestTracker.StartTarget(target, confirmedAt);
                AnglerQuestStatusText.Text = _anglerQuestTracker.GetStatus(confirmedAt);
                SaveAnglerQuestState("Angler request acceptance visually confirmed.");
                if (!_anglerQuestStateAvailable)
                    throw new InvalidOperationException("The Angler quest state could not be saved.");
                AnglerQuestSubmittedButton.IsEnabled = false;
                AnglerQuestAutomationStatusText.Text =
                    "Request accepted. Start the fishing macro; re-arm this module after the target fish is caught.";
                AddActivityLog("INFO", "ANGLER", $"Angler request accepted; tracking target '{target}'.");
            }
            else
            {
                _anglerQuestTracker.ConfirmQuestSubmitted(confirmedAt);
                AnglerQuestStatusText.Text = _anglerQuestTracker.GetStatus(confirmedAt);
                SaveAnglerQuestState("Angler fish hand-in visually confirmed.");
                if (!_anglerQuestStateAvailable)
                    throw new InvalidOperationException("The Angler cooldown could not be saved.");
                AnglerQuestSubmittedButton.IsEnabled = false;
                AnglerQuestAutomationStatusText.Text =
                    "Fish hand-in confirmed; the 120-second cooldown has started.";
                AddActivityLog("INFO", "ANGLER", "Angler fish hand-in visually confirmed; the 120-second cooldown has started.");
            }
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or ArgumentException or IOException or UnauthorizedAccessException)
        {
            AnglerQuestAutomationStatusText.Text = $"Angler state update failed: {exception.Message}";
            AddActivityLog("ERROR", "ANGLER", $"Could not update confirmed Angler action: {exception.Message}");
            throw;
        }
    }

    private void StopAnglerQuestAutomation_Click(object sender, RoutedEventArgs e) =>
        StopAnglerQuestAutomation("Stopped by the user; Angler inputs released.");

    private void StopAnglerQuestAutomation(string reason, bool log = true, bool coordinatorAlreadyStopped = false)
    {
        _anglerQuestStartRequested = false;
        _anglerQuestRequestedSpecies = null;
        var releaseFailed = false;
        try
        {
            if (!coordinatorAlreadyStopped)
                _anglerQuestAutomationCoordinator?.Stop();
            else if (_anglerQuestAutomationInput?.IsArmed == true)
                _anglerQuestAutomationInput.Disarm();
        }
        catch
        {
            releaseFailed = true;
        }

        try { _anglerQuestAutomationInput?.Dispose(); }
        catch { releaseFailed = true; }

        _anglerQuestAutomationCoordinator = null;
        _anglerQuestAutomationInput = null;
        StartAnglerQuestAutomationButton.IsEnabled = true;
        StopAnglerQuestAutomationButton.IsEnabled = false;
        AnglerQuestAutomationStatusText.Text = releaseFailed
            ? $"{reason} Warning: could not confirm every input release."
            : reason;

        if (log)
            AddActivityLog(releaseFailed ? "WARN" : "INFO", "ANGLER", AnglerQuestAutomationStatusText.Text);
    }

    private void SelectAquariumButtonRegion_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _aquariumCalibrationActionKind = ReadAquariumCalibrationAction();
            SetCalibrationAction(
                CalibrationAction.SelectAquariumButtonRegion,
                $"Drag a tight rectangle around the enabled {AquariumAutomationCoordinator.FormatAction(_aquariumCalibrationActionKind)} button.");
        }
        catch (InvalidOperationException exception)
        {
            CalibrationModeText.Text = exception.Message;
        }
    }

    private void SampleAquariumButtonColor_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _aquariumCalibrationActionKind = ReadAquariumCalibrationAction();
            SetCalibrationAction(
                CalibrationAction.SampleAquariumButtonColor,
                $"Click a clear pixel inside the enabled {AquariumAutomationCoordinator.FormatAction(_aquariumCalibrationActionKind)} button.");
        }
        catch (InvalidOperationException exception)
        {
            CalibrationModeText.Text = exception.Message;
        }
    }

    private void StopAquariumAutomation_Click(object sender, RoutedEventArgs e) =>
        StopAquariumAutomation("Stopped by the user; Auto Aquarium inputs released.");

    private void StartConnectionRecovery_Click(object sender, RoutedEventArgs e)
    {
        if (!_profile.IsReconnectButtonCalibrated)
        {
            ConnectionRecoveryStatusText.Text = "Calibrate the reconnect button region and colour first.";
            return;
        }

        if (_automationEnabled || _startRequested
            || _aquariumStartRequested || _aquariumAutomationCoordinator?.IsStarted == true)
        {
            ConnectionRecoveryStatusText.Text = "Stop fishing automation and Auto Aquarium before arming reconnect recovery.";
            return;
        }

        if (_connectionRecoveryCoordinator?.IsStarted == true || _connectionRecoveryStartRequested)
            return;

        Interlocked.Exchange(ref _emergencyStopLatch, 0);
        if (_previewCancellation is null)
            TogglePreviewLoop();

        _connectionRecoveryStartRequested = true;
        ConnectionRecoveryStatusText.Text = "Start requested. Focus Roblox; the first fresh frame will arm the watchdog.";
        UpdateConnectionRecoveryControls();
    }

    private void StopConnectionRecovery_Click(object sender, RoutedEventArgs e) =>
        StopConnectionRecovery("Stopped by the user; reconnect watchdog inputs released.");

    private void StartConnectionRecoveryNow(ScreenCaptureFrame firstFrame)
    {
        _connectionRecoveryStartRequested = false;
        RobloxInputService? input = null;
        try
        {
            if (Volatile.Read(ref _emergencyStopLatch) != 0)
                throw new InvalidOperationException("Start cancelled by emergency stop. Press Start again to re-arm.");
            if (_automationEnabled || _aquariumAutomationCoordinator?.IsStarted == true)
                throw new InvalidOperationException("Another game automation module is already armed.");
            input = new RobloxInputService(_capture);
            _connectionRecoveryInput = input;
            if (Volatile.Read(ref _emergencyStopLatch) != 0)
                throw new InvalidOperationException("Start cancelled by emergency stop. Press Start again to re-arm.");
            var coordinator = new ConnectionRecoveryCoordinator(input, _connectionRecoveryVision);
            coordinator.Start();
            if (Volatile.Read(ref _emergencyStopLatch) != 0)
            {
                input.EmergencyStop();
                throw new InvalidOperationException("Start cancelled by emergency stop. Press Start again to re-arm.");
            }
            _connectionRecoveryCoordinator = coordinator;
            coordinator.ProcessFrame(firstFrame, _profile);
            ConnectionRecoveryStatusText.Text = coordinator.LastStatus;
            AddActivityLog("INFO", "RECONNECT", "Calibrated reconnect watchdog armed.");
        }
        catch (Exception exception)
        {
            try { input?.EmergencyStop(); } catch { }
            try { input?.Dispose(); } catch { }
            _connectionRecoveryInput = null;
            _connectionRecoveryCoordinator = null;
            ConnectionRecoveryStatusText.Text = $"Reconnect watchdog could not arm: {exception.Message}";
            AddActivityLog("ERROR", "RECONNECT", $"Could not arm reconnect watchdog: {exception.Message}");
        }

        UpdateConnectionRecoveryControls();
        RefreshModuleQuickInfo();
    }

    private void StopConnectionRecovery(string reason, bool log = true, bool coordinatorAlreadyStopped = false)
    {
        _connectionRecoveryStartRequested = false;
        var releaseFailed = false;
        try
        {
            if (!coordinatorAlreadyStopped && _connectionRecoveryCoordinator?.IsStarted == true)
                _connectionRecoveryCoordinator.Stop();
            else if (_connectionRecoveryInput is not null)
                _connectionRecoveryInput.Disarm();
        }
        catch
        {
            releaseFailed = true;
        }

        try { _connectionRecoveryInput?.Dispose(); }
        catch { releaseFailed = true; }

        _connectionRecoveryCoordinator = null;
        _connectionRecoveryInput = null;
        ConnectionRecoveryStatusText.Text = releaseFailed
            ? $"{reason} Warning: could not confirm every input release."
            : reason;
        UpdateConnectionRecoveryControls();
        RefreshModuleQuickInfo();

        if (log)
            AddActivityLog(releaseFailed ? "WARN" : "INFO", "RECONNECT", ConnectionRecoveryStatusText.Text);
    }

    private void UpdateConnectionRecoveryControls()
    {
        if (StartConnectionRecoveryButton is null || StopConnectionRecoveryButton is null)
            return;

        var active = _connectionRecoveryStartRequested || _connectionRecoveryCoordinator?.IsStarted == true;
        var anotherModuleActive = _automationEnabled || _startRequested
            || _aquariumStartRequested || _aquariumAutomationCoordinator?.IsStarted == true;
        StartConnectionRecoveryButton.IsEnabled =
            !active && !anotherModuleActive && _profile.IsReconnectButtonCalibrated;
        StopConnectionRecoveryButton.IsEnabled = active;
    }

    private void StartAquariumAutomation_Click(object sender, RoutedEventArgs e)
    {
        if (_automationEnabled || _startRequested
            || _connectionRecoveryStartRequested
            || _connectionRecoveryCoordinator?.IsStarted == true)
        {
            AquariumAutomationStatusText.Text = "Stop the fishing macro and connection watchdog before arming Auto Aquarium.";
            return;
        }
        if (_aquariumAutomationCoordinator?.IsStarted == true || _aquariumStartRequested)
            return;
        if (_connectionRecoveryStartRequested || _connectionRecoveryCoordinator?.IsStarted == true)
        {
            AquariumAutomationStatusText.Text = "Stop the connection watchdog before arming Auto Aquarium.";
            return;
        }

        var options = ReadAquariumAutomationOptions();
        if (!options.HasEnabledActions)
        {
            AquariumAutomationStatusText.Text = "Enable at least one action. Restock and claim are optional and off by default.";
            return;
        }

        foreach (var action in GetAquariumActions(options))
        {
            if (!_profile.AquariumButtons.TryGetValue(action, out var calibration) || !calibration.IsCalibrated)
            {
                AquariumAutomationStatusText.Text =
                    $"Calibrate the region and enabled-button colour for {AquariumAutomationCoordinator.FormatAction(action)} first.";
                return;
            }
        }

        Interlocked.Exchange(ref _emergencyStopLatch, 0);
        if (_previewCancellation is null)
            TogglePreviewLoop();

        _aquariumStartRequested = true;
        StartAquariumAutomationButton.IsEnabled = false;
        StopAquariumAutomationButton.IsEnabled = true;
        AquariumAutomationStatusText.Text =
            "Start requested. Switch to Roblox and keep the Aquarium panel open; the first fresh frame will arm the module.";
    }

    private void StartAquariumAutomationNow()
    {
        _aquariumStartRequested = false;
        RobloxInputService? input = null;
        try
        {
            if (Volatile.Read(ref _emergencyStopLatch) != 0)
                throw new InvalidOperationException("Start cancelled by emergency stop. Press Start again to re-arm.");
            if (_automationEnabled || _connectionRecoveryStartRequested
                || _connectionRecoveryCoordinator?.IsStarted == true)
                throw new InvalidOperationException("Stop the fishing macro and connection watchdog first.");
            input = new RobloxInputService(_capture);
            _aquariumAutomationInput = input;
            if (Volatile.Read(ref _emergencyStopLatch) != 0)
                throw new InvalidOperationException("Start cancelled by emergency stop. Press Start again to re-arm.");
            var coordinator = new AquariumAutomationCoordinator(
                input,
                _aquariumButtonVision,
                AquariumActionConfirmed);
            coordinator.Start();
            if (Volatile.Read(ref _emergencyStopLatch) != 0)
            {
                input.EmergencyStop();
                throw new InvalidOperationException("Start cancelled by emergency stop. Press Start again to re-arm.");
            }

            _aquariumAutomationCoordinator = coordinator;
            AquariumAutomationStatusText.Text = coordinator.LastStatus;
            AddActivityLog("INFO", "AQUARIUM", "Auto Aquarium armed with user-calibrated action buttons.");
        }
        catch (Exception exception)
        {
            try { input?.EmergencyStop(); } catch { }
            try { input?.Dispose(); } catch { }
            _aquariumAutomationInput = null;
            _aquariumAutomationCoordinator = null;
            StartAquariumAutomationButton.IsEnabled = true;
            StopAquariumAutomationButton.IsEnabled = false;
            AquariumAutomationStatusText.Text = $"Auto Aquarium could not start: {exception.Message}";
            AddActivityLog("ERROR", "AQUARIUM", $"Auto Aquarium start failed: {exception.Message}");
        }
    }

    private void AquariumActionConfirmed(AquariumActionKind action, DateTimeOffset confirmedAt)
    {
        _aquariumScheduleState = AquariumAutomationCoordinator.MarkCompleted(
            _aquariumScheduleState, action, confirmedAt);
        RefreshAquariumPlannerDisplay(confirmedAt);
        _ = SaveAquariumScheduleAsync($"{AquariumAutomationCoordinator.FormatAction(action)} visually confirmed.");
    }

    private void StopAquariumAutomation(
        string reason,
        bool log = true,
        bool coordinatorAlreadyStopped = false)
    {
        _aquariumStartRequested = false;
        var releaseFailed = false;

        try
        {
            if (!coordinatorAlreadyStopped && _aquariumAutomationCoordinator?.IsStarted == true)
                _aquariumAutomationCoordinator.Stop();
            else if (_aquariumAutomationInput?.IsArmed == true)
                _aquariumAutomationInput.Disarm();
        }
        catch
        {
            releaseFailed = true;
        }

        try { _aquariumAutomationInput?.Dispose(); }
        catch { releaseFailed = true; }

        _aquariumAutomationCoordinator = null;
        _aquariumAutomationInput = null;
        StartAquariumAutomationButton.IsEnabled = true;
        StopAquariumAutomationButton.IsEnabled = false;
        AquariumAutomationStatusText.Text = releaseFailed
            ? $"{reason} Warning: could not confirm every input release."
            : reason;

        if (log)
            AddActivityLog(releaseFailed ? "WARN" : "INFO", "AQUARIUM", AquariumAutomationStatusText.Text);
    }

    private static VisionProfile WithAquariumButtonRegion(
        VisionProfile profile,
        AquariumActionKind action,
        NormalizedRegion region,
        int referenceWidth,
        int referenceHeight)
    {
        var buttons = profile.AquariumButtons.ToDictionary(item => item.Key, item => item.Value);
        var current = buttons.TryGetValue(action, out var existing)
            ? existing
            : new AquariumButtonCalibration();
        buttons[action] = current with { Region = region, RegionCalibrated = true };
        return profile with
        {
            AquariumButtons = buttons,
            ReferenceWidth = referenceWidth,
            ReferenceHeight = referenceHeight
        };
    }

    private static VisionProfile WithAnglerQuestButtonRegion(
        VisionProfile profile,
        AnglerQuestButtonKind kind,
        NormalizedRegion region,
        int referenceWidth,
        int referenceHeight)
    {
        var buttons = profile.AnglerQuestButtons.ToDictionary(item => item.Key, item => item.Value);
        var current = buttons.TryGetValue(kind, out var existing)
            ? existing
            : new AnglerQuestButtonCalibration();
        buttons[kind] = current with { Region = region, RegionCalibrated = true };
        return profile with
        {
            AnglerQuestButtons = buttons,
            ReferenceWidth = referenceWidth,
            ReferenceHeight = referenceHeight
        };
    }

    private static VisionProfile WithAnglerQuestButtonColor(
        VisionProfile profile,
        AnglerQuestButtonKind kind,
        HsvColorRange color,
        int referenceWidth,
        int referenceHeight)
    {
        var buttons = profile.AnglerQuestButtons.ToDictionary(item => item.Key, item => item.Value);
        var current = buttons.TryGetValue(kind, out var existing)
            ? existing
            : new AnglerQuestButtonCalibration();
        buttons[kind] = current with { ButtonColor = color };
        return profile with
        {
            AnglerQuestButtons = buttons,
            ReferenceWidth = referenceWidth,
            ReferenceHeight = referenceHeight
        };
    }

    private static VisionProfile WithAquariumButtonColor(
        VisionProfile profile,
        AquariumActionKind action,
        HsvColorRange color,
        int referenceWidth,
        int referenceHeight)
    {
        var buttons = profile.AquariumButtons.ToDictionary(item => item.Key, item => item.Value);
        var current = buttons.TryGetValue(action, out var existing)
            ? existing
            : new AquariumButtonCalibration();
        buttons[action] = current with { ButtonColor = color };
        return profile with
        {
            AquariumButtons = buttons,
            ReferenceWidth = referenceWidth,
            ReferenceHeight = referenceHeight
        };
    }

    private void RefreshAquariumPlannerDisplay(DateTimeOffset now)
    {
        AquariumGuppiesFeedStatusText.Text =
            FormatAquariumCooldown("Feed", _aquariumScheduleState.GuppiesBest.LastFedAt, now);
        AquariumGuppiesRestockStatusText.Text =
            FormatAquariumCooldown("Restock", _aquariumScheduleState.GuppiesBest.LastRestockedAt, now);
        AquariumFishChowFeedStatusText.Text =
            FormatAquariumCooldown("Feed", _aquariumScheduleState.FishChow.LastFedAt, now);
        AquariumFishChowRestockStatusText.Text =
            FormatAquariumCooldown("Restock", _aquariumScheduleState.FishChow.LastRestockedAt, now);
        AquariumSplashFeedStatusText.Text =
            FormatAquariumCooldown("Feed", _aquariumScheduleState.SplashSnacks.LastFedAt, now);
        AquariumSplashRestockStatusText.Text =
            FormatAquariumCooldown("Restock", _aquariumScheduleState.SplashSnacks.LastRestockedAt, now);

        AquariumClaimStatusText.Text = !_aquariumScheduleState.ClaimReminderEnabled
            ? "Claim reminder is disabled."
            : FormatAquariumCooldown("Reward claim", _aquariumScheduleState.LastClaimedAt, now);
    }

    private static string FormatAquariumCooldown(
        string action,
        DateTimeOffset? lastCompletedAt,
        DateTimeOffset now)
    {
        var evaluation = AquariumCooldownPolicy.Evaluate(lastCompletedAt, now);
        return evaluation.Status switch
        {
            AquariumCooldownStatus.NotTracked =>
                $"{action}: not recorded yet — mark it after completing the action in Fisch.",
            AquariumCooldownStatus.Ready =>
                $"{action}: READY · available since {evaluation.NextEligibleAt!.Value.ToLocalTime():HH:mm}.",
            AquariumCooldownStatus.CoolingDown =>
                $"{action}: {FormatAquariumCountdown(evaluation.Remaining!.Value)} remaining · " +
                $"ready at {evaluation.NextEligibleAt!.Value.ToLocalTime():HH:mm}.",
            _ => $"{action}: status unavailable."
        };
    }

    private static string FormatAquariumCountdown(TimeSpan remaining)
    {
        var totalMinutes = Math.Max(0, (int)Math.Floor(remaining.TotalMinutes));
        return $"{totalMinutes:00}:{remaining.Seconds:00}";
    }

    private async void AquariumFoodAction_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not string tag)
            return;

        var parts = tag.Split('.', StringSplitOptions.TrimEntries);
        if (parts.Length != 2
            || !Enum.TryParse<AquariumFoodType>(parts[0], ignoreCase: true, out var foodType))
        {
            AquariumPlannerStatusText.Text = "Unknown aquarium action.";
            return;
        }

        var now = DateTimeOffset.UtcNow;
        _aquariumScheduleState = parts[1].ToLowerInvariant() switch
        {
            "feed" => _aquariumScheduleState.MarkFed(foodType, now),
            "restock" => _aquariumScheduleState.MarkRestocked(foodType, now),
            _ => _aquariumScheduleState
        };

        if (parts[1].ToLowerInvariant() is not ("feed" or "restock"))
        {
            AquariumPlannerStatusText.Text = "Unknown aquarium action.";
            return;
        }

        await SaveAquariumScheduleAsync(
            $"{GetAquariumFoodLabel(foodType)} · {parts[1].ToLowerInvariant()} time recorded.");
    }

    private async void AquariumMarkClaimed_Click(object sender, RoutedEventArgs e)
    {
        _aquariumScheduleState = _aquariumScheduleState.MarkClaimed(DateTimeOffset.UtcNow);
        await SaveAquariumScheduleAsync("Aquarium reward claim time recorded.");
    }

    private async void AquariumClaimReminderCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (_isLoadingAquariumSchedule)
            return;

        _aquariumScheduleState = _aquariumScheduleState with
        {
            ClaimReminderEnabled = AquariumClaimReminderCheckBox.IsChecked == true
        };
        await SaveAquariumScheduleAsync(
            _aquariumScheduleState.ClaimReminderEnabled
                ? "Hourly aquarium claim reminder enabled."
                : "Hourly aquarium claim reminder disabled.");
    }

    private async void AquariumReadySoundCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (_isLoadingAquariumSchedule)
            return;

        _aquariumScheduleState = _aquariumScheduleState with
        {
            ReadySoundEnabled = AquariumReadySoundCheckBox.IsChecked == true
        };
        await SaveAquariumScheduleAsync(
            _aquariumScheduleState.ReadySoundEnabled
                ? "Aquarium ready sounds enabled."
                : "Aquarium ready sounds disabled.");
    }

    private async void AquariumAutomationOptions_Changed(object sender, RoutedEventArgs e)
    {
        if (_isLoadingAquariumSchedule)
            return;

        if (_aquariumAutomationCoordinator?.IsStarted == true || _aquariumStartRequested)
        {
            StopAquariumAutomation("Automation options changed; Auto Aquarium was disarmed.");
        }

        _aquariumScheduleState = _aquariumScheduleState with
        {
            AutoFeedEnabled = AquariumAutoFeedCheckBox.IsChecked == true,
            AutoRestockEnabled = AquariumAutoRestockCheckBox.IsChecked == true,
            AutoClaimEnabled = AquariumAutoClaimCheckBox.IsChecked == true
        };
        await SaveAquariumScheduleAsync("Auto Aquarium preferences saved.");
    }

    private async Task SaveAquariumScheduleAsync(string activity)
    {
        try
        {
            await _aquariumScheduleStore.SaveAsync(_aquariumScheduleState);
            AquariumPlannerStatusText.Text = $"{activity} Saved locally.";
            AddActivityLog("INFO", "AQUARIUM", activity);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            AquariumPlannerStatusText.Text = $"Could not save aquarium schedule: {exception.Message}";
            AddActivityLog("ERROR", "AQUARIUM", $"Could not save aquarium schedule: {exception.Message}");
        }

        RefreshAquariumPlannerDisplay(DateTimeOffset.UtcNow);
    }

    private static string GetAquariumFoodLabel(AquariumFoodType foodType) =>
        foodType switch
        {
            AquariumFoodType.GuppiesBest => "Guppies' Best",
            AquariumFoodType.FishChow => "Fish Chow",
            AquariumFoodType.SplashSnacks => "Splash Snacks",
            _ => foodType.ToString()
        };

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

            DashboardSessionsMetricText.Text = statistics.TotalSessions.ToString("N0");
            DashboardCatchesMetricText.Text = statistics.TotalCatches.ToString("N0");
            DashboardLegendaryMetricText.Text = statistics.LegendaryOrHigherCatches.ToString("N0");
            DashboardCatchRateMetricText.Text = $"{statistics.CatchesPerHour:F1}/h";

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

    private async void SaveCatchAlertSettings_Click(object sender, RoutedEventArgs e)
    {
        if (!TryReadOptionalPositiveDouble(MinimumAlertWeightTextBox.Text, out var minimumWeight)
            || !TryReadOptionalPositiveDecimal(MinimumAlertValueTextBox.Text, out var minimumValue))
        {
            CatchAlertSettingsStatusText.Text = "Enter positive numbers for the thresholds, or leave them blank to disable them.";
            return;
        }

        var settings = new CatchAlertSettings
        {
            WatchedSpecies = WatchSpeciesTextBox.Text.Trim(),
            AlertOnLegendaryOrHigher = AlertLegendaryCheckBox.IsChecked == true,
            AlertOnMutation = AlertMutationCheckBox.IsChecked == true,
            MinimumWeightKg = minimumWeight,
            MinimumEstimatedValue = minimumValue
        };

        try
        {
            await _catchAlertSettingsStore.SaveAsync(settings);
            CatchAlertSettingsStatusText.Text = "Alert preferences saved locally.";
            AddActivityLog("INFO", "SETTINGS", "Catch alert preferences saved.");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            CatchAlertSettingsStatusText.Text = $"Could not save alert preferences: {exception.Message}";
        }
    }

    private static bool TryReadOptionalPositiveDouble(string? text, out double? value)
    {
        value = null;
        if (string.IsNullOrWhiteSpace(text))
            return true;

        var parsed = double.TryParse(text, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.CurrentCulture, out var current)
            ? current
            : double.TryParse(text, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out current) ? current : double.NaN;
        if (!double.IsFinite(parsed) || parsed <= 0)
            return false;

        value = parsed;
        return true;
    }

    private static bool TryReadOptionalPositiveDecimal(string? text, out decimal? value)
    {
        value = null;
        if (string.IsNullOrWhiteSpace(text))
            return true;

        var parsed = decimal.TryParse(text, System.Globalization.NumberStyles.Number,
                System.Globalization.CultureInfo.CurrentCulture, out var current)
            ? current
            : decimal.TryParse(text, System.Globalization.NumberStyles.Number,
                System.Globalization.CultureInfo.InvariantCulture, out current) ? current : 0m;
        if (parsed <= 0)
            return false;

        value = parsed;
        return true;
    }

    private void AlertForCatchIfNeeded(FishCatch fishCatch)
    {
        _ = TryReadOptionalPositiveDouble(MinimumAlertWeightTextBox.Text, out var minimumWeight);
        _ = TryReadOptionalPositiveDecimal(MinimumAlertValueTextBox.Text, out var minimumValue);

        var watchedSpecies = WatchSpeciesTextBox.Text.Split(
            new[] { ',', ';', '\r', '\n' },
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var options = new CatchAlertOptions(
            watchedSpecies,
            AlertLegendaryCheckBox.IsChecked == true,
            AlertMutationCheckBox.IsChecked == true,
            minimumWeight,
            minimumValue);
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

        // Global hotkeys are registered after preferences load in Window_Loaded.
    }

    private void InitializeHotkeyControls()
    {
        foreach (var comboBox in new[]
        {
            CaptureHotkeyComboBox, ObserveHotkeyComboBox, StartHotkeyComboBox,
            StopHotkeyComboBox, ToggleUiHotkeyComboBox
        })
            comboBox.ItemsSource = AvailableFunctionKeys;
    }

    private void ApplyHotkeySettingsToControls()
    {
        CaptureHotkeyComboBox.SelectedItem = _hotkeySettings.CaptureKey;
        ObserveHotkeyComboBox.SelectedItem = _hotkeySettings.ObserveKey;
        StartHotkeyComboBox.SelectedItem = _hotkeySettings.StartKey;
        StopHotkeyComboBox.SelectedItem = _hotkeySettings.StopKey;
        ToggleUiHotkeyComboBox.SelectedItem = _hotkeySettings.ToggleUiKey;
        HideUiWhileRunningCheckBox.IsChecked = _hotkeySettings.HideUiWhileRunning;
    }

    private static uint GetVirtualKey(string key)
    {
        if (key.Length < 2 || key.Length > 3 || key[0] != 'F'
            || !int.TryParse(key.AsSpan(1), out var number) || number is < 2 or > 12)
            throw new ArgumentException("A function key between F2 and F12 is required.", nameof(key));
        return (uint)(0x70 + number - 1);
    }

    private void UnregisterGlobalHotkeys()
    {
        // Release the independent emergency listener before replacing registrations.
        _emergencyStopHotkeyListener?.Dispose();
        _emergencyStopHotkeyListener = null;

        if (_source is not null)
        {
            var handle = _source.Handle;
            Native.UnregisterHotKey(handle, CaptureHotkeyId);
            Native.UnregisterHotKey(handle, PreviewHotkeyId);
            Native.UnregisterHotKey(handle, ReelArmHotkeyId);
            Native.UnregisterHotKey(handle, EmergencyStopHotkeyId);
            Native.UnregisterHotKey(handle, ToggleUiHotkeyId);
        }

        _captureHotkeyRegistered = false;
        _previewHotkeyRegistered = false;
        _reelArmHotkeyRegistered = false;
        _emergencyStopHotkeyRegistered = false;
        _emergencyStopHotkeyUsesDispatcher = false;
        _toggleUiHotkeyRegistered = false;
    }

    private void RegisterGlobalHotkeys()
    {
        if (_source is null) return;
        UnregisterGlobalHotkeys();
        var handle = _source.Handle;
        _captureHotkeyRegistered = Native.RegisterHotKey(handle, CaptureHotkeyId, ModNoRepeat, GetVirtualKey(_hotkeySettings.CaptureKey));
        _previewHotkeyRegistered = Native.RegisterHotKey(handle, PreviewHotkeyId, ModNoRepeat, GetVirtualKey(_hotkeySettings.ObserveKey));
        _reelArmHotkeyRegistered = Native.RegisterHotKey(handle, ReelArmHotkeyId, ModNoRepeat, GetVirtualKey(_hotkeySettings.StartKey));

        // The emergency stop gets its own Win32 message pump, so a blocked WPF
        // dispatcher cannot prevent the mouse/keyboard release path from running.
        var stopKey = _hotkeySettings.StopKey;
        try
        {
            _emergencyStopHotkeyListener = new EmergencyStopHotkeyListener(
                GetVirtualKey(stopKey),
                () => EmergencyStopHotkeyPressed(stopKey));
            _emergencyStopHotkeyRegistered = _emergencyStopHotkeyListener.Start();
        }
        catch (Exception exception)
        {
            try { _emergencyStopHotkeyListener?.Dispose(); } catch { }
            _emergencyStopHotkeyListener = null;
            AddActivityLog("WARN", "HOTKEYS",
                $"Independent emergency listener failed: {exception.Message}");
        }

        if (!_emergencyStopHotkeyRegistered)
        {
            _emergencyStopHotkeyListener?.Dispose();
            _emergencyStopHotkeyListener = null;

            // Retain the normal WPF hotkey as a fallback if the dedicated listener
            // could not reserve the key. This fallback does not survive a UI hang.
            _emergencyStopHotkeyRegistered = Native.RegisterHotKey(
                handle, EmergencyStopHotkeyId, ModNoRepeat, GetVirtualKey(stopKey));
            _emergencyStopHotkeyUsesDispatcher = _emergencyStopHotkeyRegistered;
        }

        _toggleUiHotkeyRegistered = Native.RegisterHotKey(handle, ToggleUiHotkeyId, ModNoRepeat, GetVirtualKey(_hotkeySettings.ToggleUiKey));

        var stopMode = _emergencyStopHotkeyUsesDispatcher
            ? " (WPF fallback)"
            : _emergencyStopHotkeyRegistered ? " (independent)" : string.Empty;
        HotkeyStatusText.Text =
            $"Global hotkeys — {_hotkeySettings.CaptureKey}: capture {Registered(_captureHotkeyRegistered)} · " +
            $"{_hotkeySettings.ObserveKey}: observe {Registered(_previewHotkeyRegistered)} · " +
            $"{_hotkeySettings.StartKey}: start {Registered(_reelArmHotkeyRegistered)} · " +
            $"{stopKey}: stop {Registered(_emergencyStopHotkeyRegistered)}{stopMode} · " +
            $"{_hotkeySettings.ToggleUiKey}: show/hide {Registered(_toggleUiHotkeyRegistered)}";
        GlobalHotkeyStatusText.Text = HotkeyStatusText.Text;
        if (!_captureHotkeyRegistered || !_previewHotkeyRegistered || !_reelArmHotkeyRegistered
            || !_emergencyStopHotkeyRegistered || !_toggleUiHotkeyRegistered)
            AddActivityLog("WARN", "HOTKEYS", "At least one global hotkey is unavailable because another application is using that key.");
        else
            AddActivityLog("INFO", "HOTKEYS", _emergencyStopHotkeyUsesDispatcher
                ? "All five global hotkeys are registered; emergency stop is using the WPF fallback."
                : "All five global hotkeys are registered; emergency stop runs independently of WPF.");
    }

    private void EmergencyStopHotkeyPressed(string stopKey)
    {
        // Publish the latch first: pending starts and in-flight startup code must
        // not arm a new module after this emergency-stop request.
        Interlocked.Exchange(ref _emergencyStopLatch, 1);

        // This callback runs off the WPF thread. First release active inputs
        // synchronously; UI/coordinator cleanup is queued for when WPF resumes.
        try { Volatile.Read(ref _automationInput)?.EmergencyStop(); } catch { }
        try { Volatile.Read(ref _aquariumAutomationInput)?.EmergencyStop(); } catch { }
        try { Volatile.Read(ref _connectionRecoveryInput)?.EmergencyStop(); } catch { }
        try { Volatile.Read(ref _anglerQuestAutomationInput)?.EmergencyStop(); } catch { }

        try
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                // A user may have explicitly started a fresh module after the
                // emergency callback was queued. Do not let stale cleanup stop it.
                if (Volatile.Read(ref _emergencyStopLatch) == 0)
                    return;

                StopAquariumAutomation($"Emergency stop ({stopKey}): Auto Aquarium inputs released.");
                StopConnectionRecovery($"Emergency stop ({stopKey}): reconnect watchdog stopped.");
                StopAnglerQuestAutomation($"Emergency stop ({stopKey}): Angler automation stopped.");
                StopAutomation($"Emergency stop ({stopKey}): all game inputs released.");
                if (_isUiHidden) RestoreMainWindow();
            }));
        }
        catch
        {
            // Input release already happened; the dispatcher may be shutting down.
        }
    }

    private async void ApplyHotkeys_Click(object sender, RoutedEventArgs e)
    {
        if (_automationEnabled || _startRequested)
        {
            GlobalHotkeyStatusText.Text = "Stop the fishing macro before changing global hotkeys.";
            return;
        }

        try
        {
            var settings = new AppHotkeySettings
            {
                CaptureKey = CaptureHotkeyComboBox.SelectedItem as string ?? string.Empty,
                ObserveKey = ObserveHotkeyComboBox.SelectedItem as string ?? string.Empty,
                StartKey = StartHotkeyComboBox.SelectedItem as string ?? string.Empty,
                StopKey = StopHotkeyComboBox.SelectedItem as string ?? string.Empty,
                ToggleUiKey = ToggleUiHotkeyComboBox.SelectedItem as string ?? string.Empty,
                HideUiWhileRunning = HideUiWhileRunningCheckBox.IsChecked == true
            };
            settings.Validate();
            await _hotkeySettingsStore.SaveAsync(settings);
            _hotkeySettings = settings;
            RegisterGlobalHotkeys();
            GlobalHotkeyStatusText.Text = "Hotkeys saved locally and applied.";
            AddActivityLog("INFO", "SETTINGS", "Global hotkey preferences were saved.");
        }
        catch (Exception exception)
        {
            GlobalHotkeyStatusText.Text = $"Could not apply hotkeys: {exception.Message}";
            AddActivityLog("ERROR", "SETTINGS", $"Could not apply global hotkeys: {exception.Message}");
        }
    }

    private void HideMainWindow()
    {
        if (!IsVisible) return;
        _isUiHidden = true;
        Hide();
        AddActivityLog("INFO", "UI", "Window hidden. Use the configured visibility key to restore it.");
    }

    private void RestoreMainWindow()
    {
        _isUiHidden = false;
        if (!IsVisible) Show();
        if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
        Activate();
        AddActivityLog("INFO", "UI", "GafiFish window restored.");
    }

    private void ToggleMainWindowVisibility()
    {
        if (_isUiHidden || !IsVisible) RestoreMainWindow();
        else HideMainWindow();
    }

    private void AddActivityLog(string level, string source, string message)
    {
        var entry = $"{DateTime.Now:HH:mm:ss.fff}  {level,-5}  {source,-8}  {message}";
        _activityLog.Insert(0, entry);
        while (_activityLog.Count > 500)
            _activityLog.RemoveAt(_activityLog.Count - 1);

        string? persistenceError = null;
        try
        {
            _activityLogStore.Append(entry);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            persistenceError = exception.Message;
        }

        RefreshActivityLogView(scrollToNewest: true);
        if (persistenceError is not null)
            ActivityLogStatusText.Text = $"Log is visible for this run but could not be saved: {persistenceError}";
    }

    private void RefreshActivityLogView(bool scrollToNewest = false)
    {
        var search = ActivityLogSearchTextBox?.Text?.Trim() ?? string.Empty;
        var selectedLevel = ActivityLogLevelComboBox?.SelectedItem as string ?? "All levels";
        var filtered = _activityLog.Where(entry =>
        {
            var matchesSearch = search.Length == 0
                || entry.Contains(search, StringComparison.OrdinalIgnoreCase);
            var matchesLevel = selectedLevel == "All levels"
                || entry.Contains($"  {selectedLevel,-5}  ", StringComparison.OrdinalIgnoreCase);
            return matchesSearch && matchesLevel;
        }).ToArray();

        _visibleActivityLog.Clear();
        foreach (var entry in filtered)
            _visibleActivityLog.Add(entry);

        if (ActivityLogStatusText is not null)
            ActivityLogStatusText.Text = $"Showing {_visibleActivityLog.Count} of {_activityLog.Count} recent events · saved locally with automatic log rotation.";

        if (scrollToNewest && ActivityLogFollowNewestCheckBox?.IsChecked == true
            && ActivityLogList is not null && _visibleActivityLog.Count > 0)
            ActivityLogList.ScrollIntoView(_visibleActivityLog[0]);
    }

    private void ActivityLogSearch_TextChanged(object sender, TextChangedEventArgs e) =>
        RefreshActivityLogView();

    private void ActivityLogLevel_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        RefreshActivityLogView();

    private string[] GetVisibleActivityLogEntries() => _visibleActivityLog.ToArray();

    private void CopyActivityLog_Click(object sender, RoutedEventArgs e)
    {
        var visibleEntries = GetVisibleActivityLogEntries();
        try
        {
            Clipboard.SetText(string.Join(Environment.NewLine, visibleEntries));
            ActivityLogStatusText.Text = $"Copied {visibleEntries.Length} visible log entries.";
        }
        catch (Exception exception) when (exception is System.Runtime.InteropServices.ExternalException or InvalidOperationException)
        {
            ActivityLogStatusText.Text = $"Could not copy activity log: {exception.Message}";
        }
    }

    private void ExportActivityLog_Click(object sender, RoutedEventArgs e)
    {
        var visibleEntries = GetVisibleActivityLogEntries();
        var dialog = new SaveFileDialog
        {
            Title = "Export GafiFish activity log",
            Filter = "Text log (*.txt)|*.txt",
            DefaultExt = "txt",
            AddExtension = true,
            FileName = $"GafiFish-activity-{DateTime.Now:yyyyMMdd-HHmmss}.txt",
            OverwritePrompt = true
        };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            File.WriteAllText(dialog.FileName, string.Join(Environment.NewLine, visibleEntries));
            ActivityLogStatusText.Text = $"Exported {visibleEntries.Length} visible entries to {System.IO.Path.GetFileName(dialog.FileName)}.";
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            ActivityLogStatusText.Text = $"Could not export activity log: {exception.Message}";
        }
    }

    private void ClearActivityLog_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _activityLogStore.Clear();
            _activityLog.Clear();
            RefreshActivityLogView();
            AddActivityLog("INFO", "SYSTEM", "Activity log cleared.");
            ActivityLogStatusText.Text = "Saved activity history cleared. New events will be recorded.";
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            ActivityLogStatusText.Text = $"Could not clear saved activity log: {exception.Message}";
        }
    }

    private void OpenDataFolder_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var dataDirectory = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "GafiFish");
            Directory.CreateDirectory(dataDirectory);
            Process.Start(new ProcessStartInfo { FileName = dataDirectory, UseShellExecute = true });
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            ActivityLogStatusText.Text = $"Could not open the GafiFish data folder: {exception.Message}";
        }
    }

    private void RefreshModuleQuickInfo()
    {
        if (ModuleQuickInfoText is null) return;
        static string State(bool enabled) => enabled ? "ON" : "OFF";
        static string Readiness(bool enabled, bool calibrated) =>
            !enabled ? "OFF" : calibrated ? "READY" : "NEEDS CALIBRATION";
        static string TimedItem(bool enabled) => enabled ? "ON · SLOT NOT VERIFIED" : "OFF";

        var configuredTotems =
            (AutoTotemCheckBox.IsChecked == true ? 1 : 0)
            + (AutoSecondTotemCheckBox.IsChecked == true ? 1 : 0);
        var sellStatus = SellAnywhereCheckBox.IsChecked == true
            ? (_activeSessionId.HasValue && _ocr is not null
                && _profile.IsSellButtonCalibrated && _profile.IsInventoryVerificationCalibrated
                ? (_sellCoordinator?.IsStarted == true ? "ARMED" : "READY")
                : "NEEDS OCR SESSION / CALIBRATION")
            : "OFF";
        var dayNightTotemStatus = AutoDayNightTotemCheckBox.IsChecked == true
            ? (_dayNightTotemRotation is not null
                ? "ARMED"
                : _profile.IsWorldPhaseCalibrated ? "READY · TWO SLOTS" : "NEEDS PHASE CALIBRATION")
            : "OFF";
        ModuleQuickInfoText.Text =
            $"Fishing vision: {(_profile.IsFishingCalibrated ? "READY" : "NEEDS CALIBRATION")}{Environment.NewLine}" +
            $"Reel assist: {State(_automationEnabled)}{Environment.NewLine}" +
            $"Auto-cast: {Readiness(AutoCastCheckBox.IsChecked == true, _profile.IsCastCalibrated)}{Environment.NewLine}" +
            $"Auto-shake: {Readiness(AutoShakeCheckBox.IsChecked == true, _profile.IsShakeCalibrated)}{Environment.NewLine}" +
            $"Timed totems: {configuredTotems}/2 slots configured{Environment.NewLine}" +
            $"Day/night totems: {dayNightTotemStatus}{Environment.NewLine}" +
            $"Auto-potion: {TimedItem(AutoPotionCheckBox.IsChecked == true)}{Environment.NewLine}" +
            $"Sell Anywhere: {sellStatus}{Environment.NewLine}" +
            $"Reconnect watchdog: {(_connectionRecoveryCoordinator?.IsStarted == true ? "ARMED" : _profile.IsReconnectButtonCalibrated ? "READY" : "NEEDS CALIBRATION")}{Environment.NewLine}" +
            $"Hunt alerts: {(HuntDetectionEnabledCheckBox.IsChecked == true ? (_profile.IsHuntSignalCalibrated ? "ON · MONITORING" : "NEEDS CALIBRATION") : "OFF")}{Environment.NewLine}" +
            $"Angler quest: {(AnglerQuestAssistCheckBox.IsChecked == true ? "ASSIST ENABLED" : "OFF")}{Environment.NewLine}" +
            $"Local OCR session: {State(_activeSessionId.HasValue)}";
    }

    private void ModuleOption_Changed(object sender, RoutedEventArgs e) => RefreshModuleQuickInfo();

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
            Interlocked.Exchange(ref _emergencyStopLatch, 0);
            StartAutomation();
            handled = true;
        }
        else if (id == EmergencyStopHotkeyId)
        {
            EmergencyStopHotkeyPressed(_hotkeySettings.StopKey);
            handled = true;
        }
        else if (id == ToggleUiHotkeyId)
        {
            ToggleMainWindowVisibility();
            handled = true;
        }

        return IntPtr.Zero;
    }

    private static string Registered(bool registered) => registered ? "ready" : "in use";

    private void StartMacro_Click(object sender, RoutedEventArgs e)
    {
        if (_isLoadingBoundRodProfile)
        {
            AutomationStatusText.Text = "The calibration profile for this rod slot is still loading. Press Start again in a moment.";
            return;
        }

        if (_rodProfileBindingError)
        {
            AutomationStatusText.Text = "The calibration profile bound to this rod slot could not be loaded. Fix or clear the slot binding before starting.";
            return;
        }

        if (_automationEnabled || _startRequested || _aquariumStartRequested
            || _aquariumAutomationCoordinator?.IsStarted == true
            || _connectionRecoveryStartRequested
            || _connectionRecoveryCoordinator?.IsStarted == true
            || _anglerQuestStartRequested || _anglerQuestAutomationCoordinator?.IsStarted == true)
        {
            AutomationStatusText.Text = "Stop Auto Aquarium and the connection watchdog before starting the fishing macro.";
            return;
        }

        // This is a fresh, explicit user request after any previous emergency stop.
        Interlocked.Exchange(ref _emergencyStopLatch, 0);

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
        if (_aquariumStartRequested || _aquariumAutomationCoordinator is not null)
            StopAquariumAutomation("Stopped by the user; Auto Aquarium inputs released.");
        if (_anglerQuestStartRequested || _anglerQuestAutomationCoordinator is not null)
            StopAnglerQuestAutomation("Stopped by the user; Angler inputs released.");
        if (_isUiHidden) RestoreMainWindow();
    }

    private void TimedItemCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        UpdateTimedItemControls();
        RefreshModuleQuickInfo();
    }

    private void DayNightTotemCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (AutoDayNightTotemCheckBox.IsChecked == true)
        {
            AutoTotemCheckBox.IsChecked = false;
            AutoSecondTotemCheckBox.IsChecked = false;
        }

        UpdateTimedItemControls();
        DayNightTotemStatusText.Text = _profile.IsWorldPhaseCalibrated
            ? "Calibrated. The first stable phase only sets a baseline; no totem is used at startup."
            : "Calibrate the world phase region, day colour and night colour first.";
        RefreshModuleQuickInfo();
    }

    private void UpdateTimedItemControls()
    {
        var enabled = AutoCastCheckBox.IsChecked == true && !_automationEnabled;
        var dayNightEnabled = AutoDayNightTotemCheckBox.IsChecked == true;
        AutoTotemCheckBox.IsEnabled = enabled && !dayNightEnabled;
        AutoSecondTotemCheckBox.IsEnabled = enabled && !dayNightEnabled;
        AutoDayNightTotemCheckBox.IsEnabled = enabled;
        AutoPotionCheckBox.IsEnabled = enabled;
        TotemSlotComboBox.IsEnabled = enabled && !dayNightEnabled && AutoTotemCheckBox.IsChecked == true;
        TotemIntervalTextBox.IsEnabled = enabled && !dayNightEnabled && AutoTotemCheckBox.IsChecked == true;
        SecondTotemSlotComboBox.IsEnabled = enabled && !dayNightEnabled && AutoSecondTotemCheckBox.IsChecked == true;
        SecondTotemIntervalTextBox.IsEnabled = enabled && !dayNightEnabled && AutoSecondTotemCheckBox.IsChecked == true;
        DayTotemSlotComboBox.IsEnabled = enabled && dayNightEnabled;
        NightTotemSlotComboBox.IsEnabled = enabled && dayNightEnabled;
        PotionSlotComboBox.IsEnabled = enabled && AutoPotionCheckBox.IsChecked == true;
        PotionIntervalTextBox.IsEnabled = enabled && AutoPotionCheckBox.IsChecked == true;
        if (!dayNightEnabled && _dayNightTotemRotation is null && _profile.IsWorldPhaseCalibrated)
            DayNightTotemStatusText.Text = "Day/night phase calibrated. Enable the option to arm the rotation.";
        RefreshModuleQuickInfo();
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
        var enabled = AutoCastCheckBox.IsChecked == true
            && (TargetFishCheckBox.IsChecked == true || AnglerQuestAssistCheckBox.IsChecked == true)
            && !_automationEnabled;
        TargetFishTextBox.IsEnabled = enabled;
        TargetFishCatchGoalTextBox.IsEnabled = enabled;
        TargetFishProgressText.Text = $"Target progress · 0 / {ReadTargetCatchGoalOrDefault()}";
        RefreshModuleQuickInfo();
    }

    private void AnglerQuestAssistCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (_automationEnabled) return;

        if (AnglerQuestAssistCheckBox.IsChecked == true)
        {
            AutoCastCheckBox.IsChecked = true;
            TargetFishCatchGoalTextBox.Text = "1";
            AnglerQuestStatusText.Text = _anglerQuestTracker.GetStatus(DateTimeOffset.UtcNow);
        }
        else if (!_anglerQuestTracker.HasCatchToSubmit && _anglerQuestTracker.IsTracking)
        {
            AnglerQuestStatusText.Text = "Angler Quest assist disabled. The current quest tracker has been left unchanged.";
        }

        TargetFishCheckBox_Changed(TargetFishCheckBox, null!);
        RefreshModuleQuickInfo();
    }

    private void SaveAnglerQuestState(string reason)
    {
        try
        {
            _anglerQuestStateStore.Save(_anglerQuestTracker.CreateSnapshot());
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or JsonException or ArgumentException)
        {
            AnglerQuestStatusText.Text =
                $"Warning: {reason} The current state could not be saved and may not survive an application restart. {exception.Message}";
            AddActivityLog("ERROR", "ANGLER", $"Quest state persistence failed: {exception.Message}");
            _anglerQuestStateAvailable = false;
        }
    }

    private void ConfirmAnglerQuestSubmitted_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _anglerQuestTracker.ConfirmQuestSubmitted(DateTimeOffset.UtcNow);
            AnglerQuestStatusText.Text = _anglerQuestTracker.GetStatus(DateTimeOffset.UtcNow);
            SaveAnglerQuestState("Angler hand-in and cooldown confirmed.");
            AnglerQuestSubmittedButton.IsEnabled = false;
            AddActivityLog("INFO", "ANGLER", "Quest fish hand-in confirmed manually. The 120-second request cooldown has started.");
        }
        catch (InvalidOperationException exception)
        {
            AnglerQuestStatusText.Text = exception.Message;
        }
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
        var targetModeEnabled = autoCastEnabled
            && (TargetFishCheckBox.IsChecked == true || AnglerQuestAssistCheckBox.IsChecked == true);
        TargetFishTextBox.IsEnabled = targetModeEnabled;
        TargetFishCatchGoalTextBox.IsEnabled = targetModeEnabled;
        if (!autoCastEnabled)
        {
            KeepFishingUntilCatchCheckBox.IsChecked = false;
            TargetFishCheckBox.IsChecked = false;
            AnglerQuestAssistCheckBox.IsChecked = false;
            AutoTotemCheckBox.IsChecked = false;
            AutoSecondTotemCheckBox.IsChecked = false;
            AutoDayNightTotemCheckBox.IsChecked = false;
            AutoPotionCheckBox.IsChecked = false;
        }
        UpdateTimedItemControls();
        RefreshModuleQuickInfo();
    }

    private int ReadTargetCatchGoalOrDefault() =>
        int.TryParse(TargetFishCatchGoalTextBox.Text.Trim(), out var goal) && goal is >= 1 and <= 100
            ? goal
            : 1;

    private void StartAutomation()
    {
        if (_isLoadingBoundRodProfile || _rodProfileBindingError)
        {
            _startRequested = false;
            StartMacroButton.IsEnabled = true;
            StopMacroButton.IsEnabled = false;
            AutomationStatusText.Text = _isLoadingBoundRodProfile
                ? "The rod calibration profile is still loading. Press Start again after it finishes."
                : "The profile bound to this rod slot could not be loaded. Fix or clear the slot binding before starting.";
            return;
        }

        if (Volatile.Read(ref _emergencyStopLatch) != 0)
        {
            _startRequested = false;
            StartMacroButton.IsEnabled = true;
            StopMacroButton.IsEnabled = false;
            AutomationStatusText.Text = "Start cancelled by emergency stop. Press Start again to re-arm.";
            return;
        }

        if (_aquariumStartRequested || _aquariumAutomationCoordinator?.IsStarted == true
            || _connectionRecoveryStartRequested
            || _connectionRecoveryCoordinator?.IsStarted == true
            || _anglerQuestStartRequested || _anglerQuestAutomationCoordinator?.IsStarted == true)
        {
            AutomationStatusText.Text = "Stop Auto Aquarium and the connection watchdog before starting the fishing macro.";
            return;
        }

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

        var foregroundRobloxProcessId = _capture.GetForegroundRobloxProcessId();
        if (!foregroundRobloxProcessId.HasValue
            || _lastFrame is null
            || _lastFrame.ProcessId != foregroundRobloxProcessId.Value)
        {
            const string reason =
                "Start refused: Roblox is not the foreground application or the latest frame belongs to another process. No game input was sent.";
            AutomationStatusText.Text = reason;
            AddActivityLog("WARN", "INPUT-GUARD", reason);
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
        var autoSecondTotemEnabled = AutoSecondTotemCheckBox.IsChecked == true;
        var autoDayNightTotemEnabled = AutoDayNightTotemCheckBox.IsChecked == true;
        var autoPotionEnabled = AutoPotionCheckBox.IsChecked == true;
        var keepFishingUntilCatch = KeepFishingUntilCatchCheckBox.IsChecked == true;
        var anglerQuestAssistEnabled = AnglerQuestAssistCheckBox.IsChecked == true;
        var targetFishEnabled = TargetFishCheckBox.IsChecked == true || anglerQuestAssistEnabled;
        var targetFishName = TargetFishTextBox.Text.Trim();
        var targetCatchGoal = anglerQuestAssistEnabled ? 1 : ReadTargetCatchGoalOrDefault();

        if (targetFishEnabled && !anglerQuestAssistEnabled
            && (!int.TryParse(TargetFishCatchGoalTextBox.Text.Trim(), out targetCatchGoal)
                || targetCatchGoal is < 1 or > 100))
        {
            AutomationStatusText.Text = "Target catch goal must be a whole number between 1 and 100.";
            return;
        }

        if (anglerQuestAssistEnabled && !_anglerQuestStateAvailable)
        {
            AnglerQuestStatusText.Text = "Angler Quest is unavailable because its saved state could not be loaded. Repair or remove the local state file before starting another request.";
            AutomationStatusText.Text = "Angler Quest startup blocked to avoid losing an unfinished request or cooldown.";
            return;
        }

        if (anglerQuestAssistEnabled && !_anglerQuestTracker.CanStartNewQuest(DateTimeOffset.UtcNow))
        {
            AnglerQuestStatusText.Text = _anglerQuestTracker.GetStatus(DateTimeOffset.UtcNow);
            AutomationStatusText.Text = "The current Angler request or 120-second cooldown must finish before starting another quest.";
            return;
        }

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

        if (autoDayNightTotemEnabled && (autoTotemEnabled || autoSecondTotemEnabled))
        {
            AutomationStatusText.Text = "Choose either day/night totem rotation or timed totems, not both.";
            return;
        }
        if (autoDayNightTotemEnabled && !_profile.IsWorldPhaseCalibrated)
        {
            AutomationStatusText.Text = "Day/night totems require a calibrated phase region and both day/night colours.";
            return;
        }
        if ((autoTotemEnabled || autoSecondTotemEnabled || autoDayNightTotemEnabled || autoPotionEnabled) && !autoCastEnabled)
        {
            AutomationStatusText.Text = "Timed consumables require Auto-cast so they are only used between fishing cycles.";
            return;
        }
        if ((autoTotemEnabled || autoSecondTotemEnabled || autoDayNightTotemEnabled || autoPotionEnabled) && EquipRodCheckBox.IsChecked != true)
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

        if (_profile.RodMinigameMode == RodMinigameMode.ChargeMeterBonus
            && !_profile.IsRodChargeMeterCalibrated)
        {
            AutomationStatusText.Text =
                "This rod profile uses Charge Meter Bonus. Calibrate the complete meter track and its fill colour in Calibration first.";
            return;
        }

        if (autoShakeEnabled && !_profile.IsShakeCalibrated)
        {
            AutomationStatusText.Text = "Auto-shake requires a calibrated Shake region and prompt color.";
            return;
        }

        if (SellAnywhereCheckBox.IsChecked == true
            && (!_activeSessionId.HasValue
                || _ocr is null
                || !_profile.IsSellButtonCalibrated
                || !_profile.IsInventoryVerificationCalibrated))
        {
            AutomationStatusText.Text =
                "Sell Anywhere is selected, but it needs an active local OCR session plus calibrated Sell button and inventory verification regions. Start the OCR session and finish both calibrations first.";
            return;
        }

        RobloxInputService? input = null;
        try
        {
            input = new RobloxInputService(_capture);
            _automationInput = input;

            if (Volatile.Read(ref _emergencyStopLatch) != 0)
                throw new InvalidOperationException("Start cancelled by emergency stop. Press Start again to re-arm.");

            var stateMachine = new FishingStateMachine();
            if (!stateMachine.TryTransition(
                    FishingState.Preparing,
                    "User armed Reel assist with F8.",
                    DateTimeOffset.UtcNow))
            {
                throw new InvalidOperationException("Could not initialise the fishing state machine.");
            }

            var controllerSettings = RodReelPresetFactory.Create(_profile.RodPreset) with
            {
                PositiveErrorMeansHold = InvertReelDirectionCheckBox.IsChecked != true
            };
            var controller = new ReelController(controllerSettings);
            var reelCoordinator = new ReelInputCoordinator(input, controller);
            if (Volatile.Read(ref _emergencyStopLatch) != 0)
                throw new InvalidOperationException("Start cancelled by emergency stop. Press Start again to re-arm.");
            reelCoordinator.Arm();
            if (Volatile.Read(ref _emergencyStopLatch) != 0)
            {
                input.EmergencyStop();
                throw new InvalidOperationException("Start cancelled by emergency stop. Press Start again to re-arm.");
            }

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
            int? dayTotemSlot = null;
            int? nightTotemSlot = null;
            if (autoDayNightTotemEnabled)
            {
                dayTotemSlot = ReadHotbarSlot(DayTotemSlotComboBox, "day totem");
                nightTotemSlot = ReadHotbarSlot(NightTotemSlotComboBox, "night totem");
                if (dayTotemSlot == nightTotemSlot
                    || dayTotemSlot == rodSlot
                    || nightTotemSlot == rodSlot)
                    throw new InvalidOperationException("Day, night and rod must use three different hotbar slots.");
            }

            var timedActions = new List<TimedHotbarAction>();
            if (autoTotemEnabled)
            {
                var slot = ReadHotbarSlot(TotemSlotComboBox, "totem");
                if (slot == rodSlot) throw new InvalidOperationException("Totem slot and rod slot must be different.");
                timedActions.Add(new TimedHotbarAction(TimedHotbarActionKind.Totem, slot, ReadItemInterval(TotemIntervalTextBox, "Totem")));
            }
            if (autoSecondTotemEnabled)
            {
                var slot = ReadHotbarSlot(SecondTotemSlotComboBox, "second totem");
                if (slot == rodSlot || timedActions.Any(action => action.HotbarSlot == slot))
                    throw new InvalidOperationException("Each totem, potion and rod must use a different hotbar slot.");
                timedActions.Add(new TimedHotbarAction(
                    TimedHotbarActionKind.SecondaryTotem,
                    slot,
                    ReadItemInterval(SecondTotemIntervalTextBox, "Second totem")));
            }
            if (autoPotionEnabled)
            {
                var slot = ReadHotbarSlot(PotionSlotComboBox, "potion");
                if (slot == rodSlot
                    || slot == dayTotemSlot
                    || slot == nightTotemSlot
                    || timedActions.Any(action => action.HotbarSlot == slot))
                    throw new InvalidOperationException("Each totem, potion and rod must use a different hotbar slot.");
                timedActions.Add(new TimedHotbarAction(TimedHotbarActionKind.Potion, slot, ReadItemInterval(PotionIntervalTextBox, "Potion")));
            }

            SellAnywhereCoordinator? sellCoordinator = null;
            PeriodicSellSchedule? sellSchedule = null;
            var canScheduleSales =
                SellAnywhereCheckBox.IsChecked == true
                && _activeSessionId.HasValue
                && _ocr is not null
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

            if (Volatile.Read(ref _emergencyStopLatch) != 0)
            {
                input.EmergencyStop();
                throw new InvalidOperationException("Start cancelled by emergency stop. Press Start again to re-arm.");
            }

            _runtimeStateMachine = stateMachine;
            _reelCoordinator = reelCoordinator;
            _sellCoordinator = sellCoordinator;
            _sellSchedule = sellSchedule;
            _castCoordinator = castCoordinator;
            _shakeCoordinator = shakeCoordinator;
            _hotbarItemCoordinator = timedActions.Count > 0 || autoDayNightTotemEnabled
                ? new HotbarItemCoordinator(input)
                : null;
            _timedItemSchedule = timedActions.Count > 0
                ? new TimedHotbarActionSchedule(timedActions, DateTimeOffset.UtcNow)
                : null;
            _dayNightTotemRotation = autoDayNightTotemEnabled
                ? new DayNightTotemRotationCoordinator(
                    _hotbarItemCoordinator!,
                    rodSlot!.Value,
                    dayTotemSlot!.Value,
                    nightTotemSlot!.Value)
                : null;
            if (_dayNightTotemRotation is not null)
            {
                DayNightTotemStatusText.Text =
                    "Armed. Waiting for three stable frames to establish the initial phase; no item is used yet.";
            }
            _activeRodSlot = rodSlot;
            _resumeCastingAfter = DateTimeOffset.MinValue;
            _autoTotemEnabled = autoTotemEnabled;
            _autoPotionEnabled = autoPotionEnabled;
            _autoCastEnabled = autoCastEnabled;
            _autoShakeEnabled = autoShakeEnabled;
            _keepFishingUntilCatchEnabled = keepFishingUntilCatch;
            _targetFishEnabled = targetFishEnabled;
            _targetFishName = targetFishEnabled ? targetFishName : null;
            _targetFishStartedAt = targetFishEnabled ? DateTimeOffset.UtcNow : null;
            _targetCatchProgress = targetFishEnabled ? new CatchGoalProgress(targetCatchGoal) : null;
            _anglerQuestAssistEnabled = anglerQuestAssistEnabled;
            if (anglerQuestAssistEnabled)
            {
                if (_anglerQuestTracker.HasCatchToSubmit)
                    throw new InvalidOperationException("The Angler target is already caught. Hand it in before starting another Angler fishing cycle.");

                if (_anglerQuestTracker.IsTracking)
                {
                    if (!string.Equals(_anglerQuestTracker.TargetSpecies, targetFishName, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException("The entered target does not match the currently accepted Angler request.");
                    _targetFishStartedAt = _anglerQuestTracker.TargetStartedAt ?? _targetFishStartedAt;
                }
                else
                {
                    _anglerQuestTracker.StartTarget(targetFishName, _targetFishStartedAt ?? DateTimeOffset.UtcNow);
                }

                AnglerQuestStatusText.Text = _anglerQuestTracker.GetStatus(DateTimeOffset.UtcNow);
                SaveAnglerQuestState("New Angler target started.");
                if (!_anglerQuestStateAvailable)
                    throw new InvalidOperationException("Angler Quest state could not be saved. Fix local storage before starting the fishing macro.");
                AnglerQuestSubmittedButton.IsEnabled = false;
            }
            TargetFishProgressText.Text = $"Target progress · 0 / {targetCatchGoal}";
            _missedBiteRetries = new MissedBiteRetryPolicy(unlimitedRetries: keepFishingUntilCatch || targetFishEnabled);
            _automationEnabled = true;
            StartMacroButton.IsEnabled = false;
            StopMacroButton.IsEnabled = true;
            InvertReelDirectionCheckBox.IsEnabled = false;
            AutoCastCheckBox.IsEnabled = false;
            KeepFishingUntilCatchCheckBox.IsEnabled = false;
            TargetFishCheckBox.IsEnabled = false;
            TargetFishTextBox.IsEnabled = false;
            TargetFishCatchGoalTextBox.IsEnabled = false;
            AnglerQuestAssistCheckBox.IsEnabled = false;
            AnglerQuestSubmittedButton.IsEnabled = false;
            AutoTotemCheckBox.IsEnabled = false;
            AutoSecondTotemCheckBox.IsEnabled = false;
            AutoDayNightTotemCheckBox.IsEnabled = false;
            AutoPotionCheckBox.IsEnabled = false;
            TotemSlotComboBox.IsEnabled = false;
            TotemIntervalTextBox.IsEnabled = false;
            SecondTotemSlotComboBox.IsEnabled = false;
            SecondTotemIntervalTextBox.IsEnabled = false;
            DayTotemSlotComboBox.IsEnabled = false;
            NightTotemSlotComboBox.IsEnabled = false;
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
            if (autoDayNightTotemEnabled) enabledModes.Add("Day/night totem rotation");
            if (autoPotionEnabled) enabledModes.Add("Auto-potion");
            if (autoShakeEnabled) enabledModes.Add("Auto-shake");
            if (sellCoordinator is not null) enabledModes.Add("Sell Anywhere / 10 min");
            else if (SellAnywhereCheckBox.IsChecked == true)
                enabledModes.Add("Sell Anywhere unavailable (session/calibration missing)");

            AutomationStatusText.Text =
                $"Armed: {string.Join(" + ", enabledModes)}. {_hotkeySettings.StopKey} stops and releases all inputs.";
            AddActivityLog("INFO", "AUTOMATION", $"Macro armed with: {string.Join(", ", enabledModes)}.");
            RefreshModuleQuickInfo();
            if (_hotkeySettings.HideUiWhileRunning) HideMainWindow();
        }
        catch (Exception exception)
        {
            try
            {
                input?.EmergencyStop();
                input?.Disarm();
                input?.Dispose();
            }
            catch
            {
                // Best effort: never hide the original arming error.
            }

            if (ReferenceEquals(_automationInput, input))
                _automationInput = null;
            _dayNightTotemRotation = null;
            AutomationStatusText.Text = $"Reel assist could not arm: {exception.Message}";
            AddActivityLog("ERROR", "AUTOMATION", $"Could not arm macro: {exception.Message}");
            RefreshModuleQuickInfo();
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
        _dayNightTotemRotation = null;
        if (AutoDayNightTotemCheckBox.IsChecked == true)
        {
            DayNightTotemStatusText.Text = _profile.IsWorldPhaseCalibrated
                ? "Stopped. Re-arm the macro to establish a fresh day/night baseline."
                : "Day/night phase calibration is missing.";
        }
        _activeRodSlot = null;
        _resumeCastingAfter = DateTimeOffset.MinValue;
        _autoTotemEnabled = false;
        _autoPotionEnabled = false;
        _autoCastEnabled = false;
        _autoShakeEnabled = false;
        _keepFishingUntilCatchEnabled = false;
        _targetFishEnabled = false;
        _targetFishName = null;
        _targetFishStartedAt = null;
        _anglerQuestAssistEnabled = false;
        AnglerQuestAssistCheckBox.IsEnabled = true;
        StartMacroButton.IsEnabled = true;
        StopMacroButton.IsEnabled = false;
        InvertReelDirectionCheckBox.IsEnabled = true;
        AutoCastCheckBox.IsEnabled = true;
        KeepFishingUntilCatchCheckBox.IsEnabled = AutoCastCheckBox.IsChecked == true;
        TargetFishCheckBox.IsEnabled = AutoCastCheckBox.IsChecked == true;
        TargetFishTextBox.IsEnabled = AutoCastCheckBox.IsChecked == true
            && (TargetFishCheckBox.IsChecked == true || AnglerQuestAssistCheckBox.IsChecked == true);
        TargetFishCatchGoalTextBox.IsEnabled = AutoCastCheckBox.IsChecked == true
            && (TargetFishCheckBox.IsChecked == true || AnglerQuestAssistCheckBox.IsChecked == true);
        UpdateTimedItemControls();
        AutoShakeCheckBox.IsEnabled = true;
        SellAnywhereCheckBox.IsEnabled = true;
        EquipRodCheckBox.IsEnabled = true;
        RodSlotComboBox.IsEnabled = EquipRodCheckBox.IsChecked == true;
        _missedBiteRetries.Reset();

        AutomationStatusText.Text = releaseFailed
            ? $"{reason} Warning: Windows rejected an input-release event; check the mouse/keyboard state."
            : reason;
        AddActivityLog(releaseFailed ? "WARN" : "INFO", "AUTOMATION",
            releaseFailed ? $"{reason} Input release warning." : reason);
        RefreshModuleQuickInfo();
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

                var nextSaleDueAt = _sellCoordinator.NextSaleDueAt;
                var nextSaleText = nextSaleDueAt.HasValue
                    ? $" Next sale due at {nextSaleDueAt.Value.ToLocalTime():HH:mm:ss}."
                    : string.Empty;
                SellStatusText.Text = $"Sell Anywhere: {_sellCoordinator.LastStatus}{nextSaleText}";

                if (_sellCoordinator.SaleCompletedThisFrame)
                {
                    _resumeCastingAfter = DateTimeOffset.UtcNow.AddMilliseconds(750);
                    AutomationStatusText.Text =
                        "Sell Anywhere verified the sale, closed the inventory and restored the cursor; waiting before the next cast.";
                    return;
                }

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
                && controlMarker.Confidence >= 0.70
                && motion is { IsFresh: true }
                && motion.Confidence >= 0.70;

            if (reelMarkersVisible
                && (_runtimeStateMachine.CurrentState is
                    FishingState.Preparing or FishingState.Casting
                    or FishingState.WaitingForBite or FishingState.Shaking))
            {
                // If the Reel minigame appears before the cast meter reports its
                // release window, Reel owns the mouse: release the charged cast
                // immediately and move on only with fresh, confident markers.
                if (_runtimeStateMachine.CurrentState == FishingState.Casting)
                {
                    _castCoordinator?.Stop();
                    _castVision.Reset();
                }

                _shakeCoordinator?.Reset();
                _missedBiteRetries.Reset();
                if (!_runtimeStateMachine.TryTransition(
                        FishingState.Reeling,
                        "Fresh fish/control markers confirmed Reel; any active cast input was released.",
                        frame.CapturedAt))
                {
                    throw new InvalidOperationException("Could not enter Reeling after detecting fresh Reel markers.");
                }
            }

            // The phase detector runs throughout the live fishing loop. It only
            // spends a totem after a stable transition and while Preparing is safe.
            if (_dayNightTotemRotation is not null)
            {
                var phaseObservation = _worldPhaseVision.Detect(frame, _profile);
                var usedPhaseTotem = _dayNightTotemRotation.ProcessObservation(
                    phaseObservation,
                    isSafeState: _runtimeStateMachine.CurrentState == FishingState.Preparing
                        && DateTimeOffset.UtcNow >= _resumeCastingAfter);
                DayNightTotemStatusText.Text = _dayNightTotemRotation.LastStatus;

                if (usedPhaseTotem)
                {
                    _resumeCastingAfter = DateTimeOffset.UtcNow.AddSeconds(2);
                    AutomationStatusText.Text = _dayNightTotemRotation.LastStatus;
                    return;
                }
            }

            // After closing inventory or using a totem/item, allow the HUD
            // to settle before another cast or hotbar action can begin.
            if (_runtimeStateMachine.CurrentState == FishingState.Preparing
                && DateTimeOffset.UtcNow < _resumeCastingAfter)
            {
                return;
            }

            // Auto-cast is opt-in. The rod remains in the selected hotbar slot.
            // Timed consumables run only in Preparing, between completed cycles.
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
                else if (castResult.Kind is CastInputResultKind.Cancelled
                    or CastInputResultKind.TimedOut
                    or CastInputResultKind.StaleObservation)
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
                var chargeMeter = _profile.RodMinigameMode == RodMinigameMode.ChargeMeterBonus
                    ? _rodChargeMeterVision.Detect(frame, _profile)
                    : null;
                if (chargeMeter is not null)
                    RodChargeMeterStatusText.Text = chargeMeter.Status;

                var decision = _reelCoordinator.Apply(
                    _runtimeStateMachine.CurrentState,
                    fishMarker,
                    controlMarker,
                    motion,
                    chargeMeter);

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
            AddActivityLog("INFO", "CAPTURE", $"Captured frame {frame.Width}×{frame.Height} from Roblox.");
        }
        catch (Exception exception) when (
            exception is InvalidOperationException
            or System.ComponentModel.Win32Exception
            or ArgumentException)
        {
            StatusText.Text = exception.Message;
            AddActivityLog("WARN", "CAPTURE", $"Frame capture failed: {exception.Message}");
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

        StopAquariumAutomation("Observation restarted; Auto Aquarium inputs released.", log: false);
        StopFrameReplay();
        _previewCancellation = new CancellationTokenSource();
        PreviewButton.Content = "Stop observation";
        StatusText.Text = "Observation active. Bring Roblox to the foreground.";
        UpdateFrameReplayControls();
        AddActivityLog("INFO", "CAPTURE", "Continuous observation started.");
        _ = RunPreviewLoopAsync(_previewCancellation);
    }

    private void StopPreviewLoop()
    {
        if (_connectionRecoveryStartRequested || _connectionRecoveryCoordinator is not null)
            StopConnectionRecovery("Observation stopped; reconnect watchdog inputs released.");
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

        if (_aquariumStartRequested || _aquariumAutomationCoordinator is not null)
            StopAquariumAutomation("Observation stopped; Auto Aquarium inputs released.");
        if (_anglerQuestStartRequested || _anglerQuestAutomationCoordinator is not null)
            StopAnglerQuestAutomation("Observation stopped; Angler inputs released.");
        _previewCancellation?.Cancel();
        PreviewButton.Content = "Start observation";
        StatusText.Text = "Observation stopped.";
        AddActivityLog("INFO", "CAPTURE", "Continuous observation stopped.");
        UpdateFrameReplayControls();
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
                        if (token.IsCancellationRequested)
                            break;
                        DisplayFrame(frame);
                        StatusText.Text = "Observing Roblox. Alt+Tab here to inspect the latest frame.";

                        if (Volatile.Read(ref _emergencyStopLatch) != 0)
                        {
                            if (_aquariumStartRequested)
                                StopAquariumAutomation("Pending Auto Aquarium start cancelled by emergency stop.");
                            if (_connectionRecoveryStartRequested)
                                StopConnectionRecovery("Pending reconnect start cancelled by emergency stop.");
                            if (_anglerQuestStartRequested || _anglerQuestAutomationCoordinator is not null)
                                StopAnglerQuestAutomation("Emergency stop; pending or active Angler automation stopped.");
                            if (_startRequested)
                            {
                                _startRequested = false;
                                StartMacroButton.IsEnabled = true;
                                StopMacroButton.IsEnabled = false;
                                AutomationStatusText.Text = "Pending macro start cancelled by emergency stop. Press Start again.";
                            }
                        }

                        if (Volatile.Read(ref _emergencyStopLatch) == 0
                            && _aquariumStartRequested && !_automationEnabled)
                            StartAquariumAutomationNow();

                        if (Volatile.Read(ref _emergencyStopLatch) == 0
                            && _connectionRecoveryStartRequested && !_automationEnabled
                            && !_aquariumStartRequested)
                            StartConnectionRecoveryNow(frame);

                        if (Volatile.Read(ref _emergencyStopLatch) == 0 && _anglerQuestStartRequested)
                            StartAnglerQuestAutomationNow(frame);

                        if (Volatile.Read(ref _emergencyStopLatch) == 0
                            && _startRequested && !_automationEnabled)
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
                            StopAutomation($"Screen capture failed; all game inputs were released: {exception.Message}");
                        if (_aquariumStartRequested || _aquariumAutomationCoordinator is not null)
                            StopAquariumAutomation($"Screen capture failed; Auto Aquarium stopped: {exception.Message}");
                        if (_connectionRecoveryStartRequested || _connectionRecoveryCoordinator is not null)
                            StopConnectionRecovery($"Screen capture failed; reconnect watchdog stopped: {exception.Message}");
                        if (_anglerQuestStartRequested || _anglerQuestAutomationCoordinator is not null)
                            StopAnglerQuestAutomation($"Screen capture failed; Angler automation stopped: {exception.Message}");
                    }
                }
                else
                {
                    if (_automationEnabled)
                        StopAutomation("Roblox lost foreground focus; all Reel inputs were released.");
                    if (_aquariumAutomationCoordinator?.IsStarted == true)
                        StopAquariumAutomation("Roblox lost foreground focus; Auto Aquarium stopped and inputs released.");
                    if (_connectionRecoveryCoordinator?.IsStarted == true)
                        StopConnectionRecovery("Roblox lost foreground focus; reconnect watchdog stopped and inputs released.");
                    if (_anglerQuestStartRequested || _anglerQuestAutomationCoordinator?.IsStarted == true)
                        StopAnglerQuestAutomation("Roblox lost foreground focus; Angler inputs released.");
                }

                var processingMilliseconds = Stopwatch.GetElapsedTime(loopStartedAt).TotalMilliseconds;
                var adaptiveDelayMilliseconds = CaptureCadencePolicy.CalculateDelayMilliseconds(
                    GetSelectedCapturePerformanceMode(),
                    processingMilliseconds);
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
                PreviewButton.Content = "Start observation";
                UpdateFrameReplayControls();
            }
        }
    }


    private void SaveCurrentFrame_Click(object sender, RoutedEventArgs e)
    {
        if (_lastFrame is null)
        {
            FrameReplayStatusText.Text = "Capture a Roblox frame with F6 before saving it.";
            return;
        }

        var dialog = new SaveFileDialog
        {
            Title = "Save current GafiFish frame",
            Filter = "PNG image (*.png)|*.png",
            DefaultExt = "png",
            AddExtension = true,
            FileName = $"GafiFish-frame-{DateTime.Now:yyyyMMdd-HHmmss-fff}.png",
            OverwritePrompt = true
        };
        if (dialog.ShowDialog(this) != true)
            return;

        try
        {
            var frame = _lastFrame;
            var bitmap = BitmapSource.Create(
                frame.Width, frame.Height, 96, 96,
                PixelFormats.Bgra32, null, frame.BgraPixels, frame.Stride);
            bitmap.Freeze();

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var stream = File.Create(dialog.FileName);
            encoder.Save(stream);
            FrameReplayStatusText.Text =
                $"Saved {frame.Width}×{frame.Height} frame to {System.IO.Path.GetFileName(dialog.FileName)}.";
        }
        catch (Exception exception)
        {
            FrameReplayStatusText.Text = $"Could not save frame: {exception.Message}";
        }
    }

    private void LoadFrameReplay_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Load ordered Fisch frame images",
            Filter = "Frame images (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp",
            Multiselect = true,
            CheckFileExists = true
        };
        if (dialog.ShowDialog(this) != true)
            return;

        try
        {
            var sequence = FrameReplaySequence.Create(dialog.FileNames);

            _videoReplaySession?.Dispose();
            _videoReplaySession = null;
            _videoReplayFileName = null;
            _videoReplayFrameStep = 1;
            _frameReplayTimer.Interval = TimeSpan.FromMilliseconds(120);
            StopFrameReplay();
            if (_previewCancellation is not null)
                StopPreviewLoop();
            if (_automationEnabled)
                StopAutomation("Offline frame replay started; all game inputs were released.");

            _frameReplaySequence = sequence;
            _motionTracker.Reset();
            _lastDisplayedFrameAt = DateTimeOffset.MinValue;
            _smoothedCaptureFps = 0;

            if (DisplayCurrentReplayFrame())
            {
                AddActivityLog("INFO", "REPLAY",
                    $"Loaded offline image sequence with {sequence.Count} frames.");
            }
            UpdateFrameReplayControls();
        }
        catch (Exception exception)
        {
            StopFrameReplay();
            FrameReplayStatusText.Text = $"Could not load frame sequence: {exception.Message}";
            UpdateFrameReplayControls();
        }
    }

    private void LoadVideoReplay_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Load Fisch video for offline replay",
            Filter = "Video files (*.mp4;*.m4v;*.avi;*.mov;*.mkv;*.wmv;*.webm)|*.mp4;*.m4v;*.avi;*.mov;*.mkv;*.wmv;*.webm|All files (*.*)|*.*",
            Multiselect = false,
            CheckFileExists = true
        };
        if (dialog.ShowDialog(this) != true)
            return;

        try
        {
            var reader = new OpenCvVideoFrameReader(dialog.FileName);
            VideoReplaySession session;
            try
            {
                session = new VideoReplaySession(reader);
            }
            catch
            {
                reader.Dispose();
                throw;
            }

            StopFrameReplay();
            if (_previewCancellation is not null)
                StopPreviewLoop();
            if (_automationEnabled)
                StopAutomation("Offline video replay started; all game inputs were released.");
            StopAquariumAutomation("Offline video replay started; Aquarium inputs were released.", log: false);

            _videoReplaySession?.Dispose();
            _videoReplaySession = session;
            _videoReplayFileName = System.IO.Path.GetFileName(dialog.FileName);
            _frameReplaySequence = null;
            _videoReplayFrameStep = session.PlaybackFrameStep;
            _frameReplayTimer.Interval = session.PlaybackInterval;
            _motionTracker.Reset();
            _lastDisplayedFrameAt = DateTimeOffset.MinValue;
            _smoothedCaptureFps = 0;

            if (!session.TryMoveTo(0, out _))
                throw new InvalidDataException("The video opened, but its first frame could not be decoded.");

            if (DisplayCurrentReplayFrame())
            {
                AddActivityLog("INFO", "REPLAY",
                    $"Loaded offline video '{_videoReplayFileName}' with {session.FrameCount} frames at {session.FramesPerSecond:F1} FPS.");
            }
            UpdateFrameReplayControls();
        }
        catch (Exception exception)
        {
            StopFrameReplay();
            _videoReplaySession?.Dispose();
            _videoReplaySession = null;
            _videoReplayFileName = null;
            FrameReplayStatusText.Text = $"Could not load video replay: {exception.Message}";
            UpdateFrameReplayControls();
        }
    }

    private void ReplayPrevious_Click(object sender, RoutedEventArgs e)
    {
        StopFrameReplay();
        if (_videoReplaySession is not null)
        {
            if (!_videoReplaySession.TryMovePrevious(out _))
                return;
        }
        else if (_frameReplaySequence is null || !_frameReplaySequence.MovePrevious())
        {
            return;
        }

        _motionTracker.Reset();
        _lastDisplayedFrameAt = DateTimeOffset.MinValue;
        _smoothedCaptureFps = 0;
        _ = DisplayCurrentReplayFrame();
        UpdateFrameReplayControls();
    }

    private void ReplayNext_Click(object sender, RoutedEventArgs e)
    {
        StopFrameReplay();
        if (_videoReplaySession is not null)
        {
            if (!_videoReplaySession.TryMoveNext(out _))
                return;
        }
        else if (_frameReplaySequence is null || !_frameReplaySequence.MoveNext())
        {
            return;
        }

        _ = DisplayCurrentReplayFrame();
        UpdateFrameReplayControls();
    }

    private void ReplayPlay_Click(object sender, RoutedEventArgs e)
    {
        if (_frameReplaySequence is null && _videoReplaySession is null)
            return;

        if (_frameReplayTimer.IsEnabled)
        {
            StopFrameReplay();
            FrameReplayStatusText.Text += " · paused";
            return;
        }

        var hasNext = _videoReplaySession is not null
            ? _videoReplaySession.HasNext
            : _frameReplaySequence?.HasNext == true;

        if (!hasNext)
        {
            _motionTracker.Reset();
            _lastDisplayedFrameAt = DateTimeOffset.MinValue;
            _smoothedCaptureFps = 0;

            if (_videoReplaySession is not null)
            {
                if (!_videoReplaySession.TryMoveTo(0, out _))
                    return;
            }
            else
            {
                _frameReplaySequence!.Restart();
            }

            if (!DisplayCurrentReplayFrame())
                return;
        }

        _frameReplayTimer.Start();
        UpdateFrameReplayControls();
    }

    private void FrameReplayTimer_Tick(object? sender, EventArgs e)
    {
        if (_videoReplaySession is not null)
        {
            if (!_videoReplaySession.HasNext)
            {
                StopFrameReplay();
                UpdateFrameReplayControls();
                return;
            }

            var nextIndex = Math.Min(
                _videoReplaySession.CurrentIndex + _videoReplayFrameStep,
                _videoReplaySession.FrameCount - 1);
            if (!_videoReplaySession.TryMoveTo(nextIndex, out _))
            {
                StopFrameReplay();
                FrameReplayStatusText.Text = "Video replay stopped because the next frame could not be decoded.";
                UpdateFrameReplayControls();
                return;
            }
        }
        else if (_frameReplaySequence is null || !_frameReplaySequence.MoveNext())
        {
            StopFrameReplay();
            UpdateFrameReplayControls();
            return;
        }

        if (!DisplayCurrentReplayFrame())
        {
            StopFrameReplay();
            UpdateFrameReplayControls();
            return;
        }

        var hasNext = _videoReplaySession is not null
            ? _videoReplaySession.HasNext
            : _frameReplaySequence?.HasNext == true;
        if (!hasNext)
            StopFrameReplay();

        UpdateFrameReplayControls();
    }

    private void StopFrameReplay()
    {
        _frameReplayTimer.Stop();
        if (ReplayPlayButton is not null)
            ReplayPlayButton.Content = "Play";
    }

    private void UpdateFrameReplayControls()
    {
        var sequence = _frameReplaySequence;
        var hasReplay = sequence is not null || _videoReplaySession is not null;
        var hasPrevious = _videoReplaySession is not null
            ? _videoReplaySession.HasPrevious
            : sequence?.HasPrevious == true;
        var hasNext = _videoReplaySession is not null
            ? _videoReplaySession.HasNext
            : sequence?.HasNext == true;
        var offlineModeAvailable = _previewCancellation is null && !_automationEnabled;

        ReplayPreviousButton.IsEnabled = offlineModeAvailable && hasReplay && hasPrevious;
        ReplayNextButton.IsEnabled = offlineModeAvailable && hasReplay && hasNext;
        ReplayPlayButton.IsEnabled = offlineModeAvailable && hasReplay
            && (_videoReplaySession?.FrameCount ?? sequence?.Count ?? 0) > 1;
        ReplayPlayButton.Content = _frameReplayTimer.IsEnabled ? "Pause" : "Play";
    }

    private bool DisplayCurrentReplayFrame()
    {
        if (_videoReplaySession is not null && _videoReplaySession.CurrentFrame is { } videoFrame)
        {
            var frame = new ScreenCaptureFrame(
                videoFrame.Width,
                videoFrame.Height,
                videoFrame.Stride,
                videoFrame.BgraPixels,
                DateTimeOffset.UtcNow,
                $"Offline video replay · {_videoReplayFileName ?? "video"}",
                0);

            DisplayFrame(frame, allowAutomation: false);
            FrameReplayStatusText.Text =
                $"Preview-only video replay · {_videoReplaySession.CurrentIndex + 1}/{_videoReplaySession.FrameCount} · " +
                $"{_videoReplayFileName ?? "video"} · {_videoReplaySession.FramesPerSecond:F1} FPS";
            return true;
        }

        if (_frameReplaySequence is null)
            return false;

        try
        {
            var path = _frameReplaySequence.CurrentPath;
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            var decoder = BitmapDecoder.Create(
                stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            if (decoder.Frames.Count == 0)
                throw new InvalidDataException("The selected image contains no frame.");

            var source = decoder.Frames[0];
            if (source.PixelWidth is < 64 or > 8192 || source.PixelHeight is < 64 or > 8192)
                throw new InvalidDataException("Frame dimensions must be between 64 and 8192 pixels.");

            BitmapSource bgra = source.Format == PixelFormats.Bgra32
                ? source
                : new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
            bgra.Freeze();

            var stride = checked(bgra.PixelWidth * 4);
            var pixels = new byte[checked(stride * bgra.PixelHeight)];
            bgra.CopyPixels(pixels, stride, 0);
            var frame = new ScreenCaptureFrame(
                bgra.PixelWidth,
                bgra.PixelHeight,
                stride,
                pixels,
                DateTimeOffset.UtcNow,
                $"Offline replay · {System.IO.Path.GetFileName(path)}",
                0);

            DisplayFrame(frame, allowAutomation: false);
            FrameReplayStatusText.Text =
                $"Preview-only replay · {_frameReplaySequence.CurrentIndex + 1}/{_frameReplaySequence.Count} · {System.IO.Path.GetFileName(path)}";
            return true;
        }
        catch (Exception exception)
        {
            FrameReplayStatusText.Text = $"Could not decode replay frame: {exception.Message}";
            return false;
        }
    }

    private void DisplayFrame(ScreenCaptureFrame frame, bool allowAutomation = true)
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

        if (allowAutomation && HuntDetectionEnabledCheckBox.IsChecked == true)
        {
            if (!_profile.IsHuntSignalCalibrated)
            {
                HuntDetectionStatusText.Text = "Hunt alerts enabled, but calibration is missing.";
            }
            else
            {
                var huntObservation = _huntSignalVision.Detect(frame, _profile);
                if (_huntDetectionCoordinator.ProcessObservation(huntObservation))
                {
                    System.Media.SystemSounds.Asterisk.Play();
                    AddActivityLog("INFO", "HUNT", "A calibrated hunt cue was detected. No game input was sent.");
                }
                HuntDetectionStatusText.Text = _huntDetectionCoordinator.LastStatus;
            }
        }

        if (allowAutomation)
        {
            if (_connectionRecoveryCoordinator is { IsStarted: true } connectionRecoveryCoordinator)
            {
                connectionRecoveryCoordinator.ProcessFrame(frame, _profile);
                ConnectionRecoveryStatusText.Text = connectionRecoveryCoordinator.LastStatus;
                if (!connectionRecoveryCoordinator.IsStarted)
                    StopConnectionRecovery(connectionRecoveryCoordinator.LastStatus, coordinatorAlreadyStopped: true);
            }

            ProcessAutomationFrame(
                frame,
                visionResult.FishMarker,
                visionResult.ControlMarker,
                motion,
                castObservation,
                shakeResult.Prompts);

            TryStartCatchRecognition(frame);
            TryStartRodNameRecognition(frame);

            if (_aquariumAutomationCoordinator is { IsStarted: true } aquariumCoordinator)
            {
                aquariumCoordinator.ProcessFrame(
                    frame,
                    _profile,
                    _aquariumScheduleState,
                    ReadAquariumAutomationOptions());
                AquariumAutomationStatusText.Text = aquariumCoordinator.LastStatus;

                if (!aquariumCoordinator.IsStarted)
                    StopAquariumAutomation(aquariumCoordinator.LastStatus, log: true, coordinatorAlreadyStopped: true);
            }

            if (_anglerQuestAutomationCoordinator is { IsStarted: true } anglerCoordinator)
            {
                anglerCoordinator.ProcessFrame(
                    frame, _profile, _anglerQuestTracker, _anglerQuestRequestedSpecies ?? TargetFishTextBox.Text);
                AnglerQuestAutomationStatusText.Text = anglerCoordinator.LastStatus;

                if (!anglerCoordinator.IsStarted)
                    StopAnglerQuestAutomation(
                        anglerCoordinator.LastStatus, log: true, coordinatorAlreadyStopped: true);
            }
        }
        else
        {
            AutomationStatusText.Text =
                "Offline replay active · game inputs and OCR session writes are disabled.";
            AquariumAutomationStatusText.Text = "Offline replay · Auto Aquarium is disabled.";
        }
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

    private static string GetProfilePath(string name)
    {
        var localData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.Equals(name.Trim(), "Default", StringComparison.OrdinalIgnoreCase))
            return System.IO.Path.Combine(localData, "GafiFish", "vision-profile.json");

        return System.IO.Path.Combine(
            localData, "GafiFish", "rod-profiles", $"{ProfileSlug(name)}.json");
    }

    private static VisionProfileStore CreateProfileStore(string name)
    {
        if (string.Equals(name.Trim(), "Default", StringComparison.OrdinalIgnoreCase))
            return new VisionProfileStore();
        return new VisionProfileStore(GetProfilePath(name));
    }

    private void RefreshRodProfileBindingStatus()
    {
        if (!IsLoaded)
            return;

        if (_rodProfileBindingError)
        {
            RodProfileBindingStatusText.Text =
                "The profile bound to this slot could not be loaded. Rebind it after saving the profile, or clear the slot binding.";
            return;
        }

        if (EquipRodCheckBox.IsChecked != true)
        {
            RodProfileBindingStatusText.Text =
                "Enable “Equip my rod” and choose its hotbar slot to bind a profile.";
            return;
        }

        try
        {
            var slot = ReadHotbarSlot(RodSlotComboBox, "rod");
            RodProfileBindingStatusText.Text = _rodProfileBindings.SlotProfiles.TryGetValue(slot, out var name)
                ? $"Hotbar slot {slot} → profile “{name}”. It loads automatically when this rod slot is selected."
                : $"Hotbar slot {slot} has no profile binding. The currently loaded profile will be used.";
        }
        catch (InvalidOperationException exception)
        {
            RodProfileBindingStatusText.Text = exception.Message;
        }
    }

    private void RefreshRodReelPresetControls()
    {
        _isLoadingRodReelPreset = true;
        try
        {
            RodReelPresetComboBox.SelectedValue = _profile.RodPreset.ToString();
            RodMinigameModeComboBox.SelectedValue = _profile.RodMinigameMode.ToString();
            RodChargeMeterFillFromRightCheckBox.IsChecked = _profile.RodChargeMeterFillFromRight;
        }
        finally
        {
            _isLoadingRodReelPreset = false;
        }

        RodReelPresetStatusText.Text = _profile.RodPreset switch
        {
            RodReelPreset.Balanced => "Balanced · default Reel response.",
            RodReelPreset.Precision => "Precision · quicker correction and a narrower deadband.",
            RodReelPreset.HeavyFish => "Heavy fish · more smoothing and a wider correction window.",
            RodReelPreset.Stable => "Stable · slower, steadier corrections.",
            _ => "Unknown preset."
        };
    }

    private async void RodReelPresetComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoadingRodReelPreset || !IsLoaded
            || RodReelPresetComboBox.SelectedValue is not string selected
            || !Enum.TryParse<RodReelPreset>(selected, ignoreCase: true, out var preset)
            || !Enum.IsDefined(preset))
            return;

        if (_profile.RodPreset == preset)
            return;

        if (_automationEnabled || _startRequested || _isLoadingBoundRodProfile)
        {
            RodNameRecognitionStatusText.Text =
                "Stop the macro and finish loading the active rod profile before changing its Reel preset.";
            RefreshRodReelPresetControls();
            return;
        }

        _profile = _profile with { RodPreset = preset };
        await SaveProfileAsync($"Rod Reel preset changed to {preset}.");
        RefreshRodReelPresetControls();
    }

    private async void RodMinigameModeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoadingRodReelPreset || !IsLoaded
            || RodMinigameModeComboBox.SelectedValue is not string selected
            || !Enum.TryParse<RodMinigameMode>(selected, ignoreCase: true, out var mode)
            || !Enum.IsDefined(mode))
            return;

        if (_profile.RodMinigameMode == mode)
            return;

        if (_automationEnabled || _startRequested || _isLoadingBoundRodProfile)
        {
            RodNameRecognitionStatusText.Text =
                "Stop the macro and finish loading the active rod profile before changing its minigame mode.";
            RefreshRodReelPresetControls();
            return;
        }

        _profile = _profile with { RodMinigameMode = mode };
        await SaveProfileAsync($"Rod minigame mode changed to {mode}.");
        RodMinigameModeStatusText.Text = mode == RodMinigameMode.ChargeMeterBonus
            ? _profile.IsRodChargeMeterCalibrated
                ? "Charge Meter Bonus · calibrated. The macro releases at 80% while the fish is in the control bar."
                : "Charge Meter Bonus · calibrate the meter region and fill colour before starting."
            : "Standard Reel · uses the shared marker-based controller.";
        RefreshRodReelPresetControls();
    }

    private async void RodChargeMeterFillDirection_Changed(object sender, RoutedEventArgs e)
    {
        if (_isLoadingRodReelPreset || !IsLoaded)
            return;

        if (_automationEnabled || _startRequested || _isLoadingBoundRodProfile)
        {
            RefreshRodReelPresetControls();
            RodMinigameModeStatusText.Text = "Stop the macro before changing charge-meter calibration.";
            return;
        }

        var fromRight = RodChargeMeterFillFromRightCheckBox.IsChecked == true;
        if (_profile.RodChargeMeterFillFromRight == fromRight)
            return;

        _profile = _profile with { RodChargeMeterFillFromRight = fromRight };
        await SaveProfileAsync("Rod charge-meter fill direction updated.");
        RefreshRodReelPresetControls();
    }

    private void SelectRodChargeMeterRegion_Click(object sender, RoutedEventArgs e) =>
        SetCalibrationAction(
            CalibrationAction.SelectRodChargeMeterRegion,
            "Select the full horizontal charge-meter track, from 0% to 100%. Avoid its border and labels.");

    private void SampleRodChargeMeterColor_Click(object sender, RoutedEventArgs e) =>
        SetCalibrationAction(
            CalibrationAction.SampleRodChargeMeterColor,
            "Sample a clear pixel from the filled portion of the rod charge meter.");

    private async void BindDetectedRodNameToProfile_Click(object sender, RoutedEventArgs e)
    {
        if (_automationEnabled || _startRequested || _isLoadingBoundRodProfile)
        {
            RodNameRecognitionStatusText.Text = "Stop the macro before changing rod-name profile mappings.";
            return;
        }

        var rawName = RodNameMatcher.CleanCandidate(RecognizedRodNameTextBox.Text);
        var normalizedName = RodNameMatcher.NormalizeName(rawName);
        var profileName = _activeProfileName.Trim();
        if (normalizedName.Length is < 3 or > 64)
        {
            RodNameRecognitionStatusText.Text = "Recognize a clear rod name before binding it to a profile.";
            return;
        }

        if (string.IsNullOrWhiteSpace(profileName)
            || !string.Equals(profileName, RodProfileNameTextBox.Text.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            RodNameRecognitionStatusText.Text =
                "Load or save the calibration profile you want to associate before binding the detected rod name.";
            return;
        }

        try
        {
            await _profileStore.SaveAsync(_profile);
            var nameMappings = new Dictionary<string, string>(
                _rodProfileBindings.RodNameProfiles, StringComparer.OrdinalIgnoreCase)
            {
                [normalizedName] = profileName
            };
            var updated = new RodProfileSlotBindings
            {
                SlotProfiles = new Dictionary<int, string>(_rodProfileBindings.SlotProfiles),
                RodNameProfiles = nameMappings
            };
            await _rodProfileBindingStore.SaveAsync(updated);
            _rodProfileBindings = updated;
            _pendingRodNameKey = normalizedName;
            _pendingRodNameFrames = 0;
            RodNameRecognitionStatusText.Text =
                $"“{rawName}” is now associated with profile “{profileName}”. Automatic recognition requires two stable reads.";
            AddActivityLog("INFO", "PROFILE", $"Rod OCR name '{rawName}' mapped to profile '{profileName}'.");
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        {
            RodNameRecognitionStatusText.Text = $"Could not save rod-name mapping: {exception.Message}";
        }
    }

    private void SelectRodNameRegion_Click(object sender, RoutedEventArgs e) =>
        SetCalibrationAction(
            CalibrationAction.SelectRodNameRegion,
            "Select a tight rectangle containing only the equipped rod name label. Keep it clear of other HUD text.");

    private async void RecognizeRodNow_Click(object sender, RoutedEventArgs e)
    {
        if (_lastFrame is null)
        {
            RodNameRecognitionStatusText.Text = "Capture a fresh Roblox frame with F6 first.";
            return;
        }

        if (!_profile.RodNameRegionCalibrated)
        {
            RodNameRecognitionStatusText.Text = "Calibrate the rod-name OCR region first.";
            return;
        }

        await RecognizeRodNameFrameAsync(_lastFrame, automatic: false);
    }

    private async void AutoDetectRodCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded)
            return;

        if (AutoDetectRodCheckBox.IsChecked != true)
        {
            _pendingRodNameKey = null;
            _pendingRodNameFrames = 0;
            RodNameRecognitionStatusText.Text = "Automatic rod recognition is disabled.";
            return;
        }

        if (!_profile.RodNameRegionCalibrated)
        {
            AutoDetectRodCheckBox.IsChecked = false;
            RodNameRecognitionStatusText.Text =
                "Select and save the rod-name OCR region before enabling automatic recognition.";
            return;
        }

        RodNameRecognitionStatusText.Text = "Preparing local OCR for automatic rod recognition…";
        try
        {
            await EnsureLocalOcrEngineAsync();
            RodNameRecognitionStatusText.Text =
                "Automatic rod recognition is ready. Keep the equipped rod name visible while observing.";
        }
        catch (Exception exception)
        {
            AutoDetectRodCheckBox.IsChecked = false;
            RodNameRecognitionStatusText.Text =
                $"Could not initialise local rod OCR: {exception.Message}";
        }
    }

    private void TryStartRodNameRecognition(ScreenCaptureFrame frame)
    {
        if (AutoDetectRodCheckBox.IsChecked != true
            || !_profile.RodNameRegionCalibrated
            || _ocr is null
            || _automationEnabled
            || _startRequested
            || frame.CapturedAt - _lastRodNameOcrAt < TimeSpan.FromSeconds(1.5)
            || Volatile.Read(ref _rodNameOcrInProgress) != 0)
            return;

        _lastRodNameOcrAt = frame.CapturedAt;
        _ = RecognizeRodNameFrameAsync(frame, automatic: true);
    }

    private async Task RecognizeRodNameFrameAsync(ScreenCaptureFrame frame, bool automatic)
    {
        if (Interlocked.CompareExchange(ref _rodNameOcrInProgress, 1, 0) != 0)
            return;

        var selectionVersion = Volatile.Read(ref _rodProfileSelectionVersion);
        var region = _profile.RodNameRegion;
        try
        {
            var ocr = await EnsureLocalOcrEngineAsync();
            var result = await Task.Run(() => ocr.Recognize(frame, region));

            if (!IsLoaded || selectionVersion != Volatile.Read(ref _rodProfileSelectionVersion))
                return;
            if (_lastFrame is not null && _lastFrame.CapturedAt - frame.CapturedAt > TimeSpan.FromSeconds(2))
                return;

            var candidate = RodNameMatcher.CleanCandidate(result.Text);
            RecognizedRodNameTextBox.Text = candidate;
            var normalized = RodNameMatcher.NormalizeName(candidate);
            if (result.Confidence < 0.72 || normalized.Length < 3)
            {
                _pendingRodNameKey = null;
                _pendingRodNameFrames = 0;
                RodNameRecognitionStatusText.Text =
                    $"Rod name was not clear enough (OCR {result.Confidence:P0}). Keep the label unobstructed and recalibrate if needed.";
                return;
            }

            if (!automatic)
            {
                RodNameRecognitionStatusText.Text =
                    $"Detected “{candidate}” · OCR {result.Confidence:P0}. Bind it to the currently active profile below.";
                return;
            }

            if (string.Equals(_pendingRodNameKey, normalized, StringComparison.Ordinal))
                _pendingRodNameFrames++;
            else
            {
                _pendingRodNameKey = normalized;
                _pendingRodNameFrames = 1;
            }

            if (_pendingRodNameFrames < 2)
            {
                RodNameRecognitionStatusText.Text =
                    $"Possible rod: “{candidate}” · confirming {_pendingRodNameFrames}/2 stable reads.";
                return;
            }

            if (!_rodProfileBindings.RodNameProfiles.TryGetValue(normalized, out var profileName))
            {
                RodNameRecognitionStatusText.Text =
                    $"Recognized “{candidate}”, but it has no profile mapping. Bind it to the correct active profile below.";
                return;
            }

            if (string.Equals(profileName, _activeProfileName, StringComparison.OrdinalIgnoreCase))
            {
                RodNameRecognitionStatusText.Text =
                    $"Rod “{candidate}” confirmed. Profile “{profileName}” is already active.";
                return;
            }

            await ApplyRecognizedRodProfileAsync(profileName, candidate, selectionVersion);
        }
        catch (Exception exception)
        {
            if (IsLoaded)
                RodNameRecognitionStatusText.Text = $"Rod-name OCR failed: {exception.Message}";
        }
        finally
        {
            Interlocked.Exchange(ref _rodNameOcrInProgress, 0);
        }
    }

    private async Task ApplyRecognizedRodProfileAsync(
        string profileName,
        string recognizedName,
        int selectionVersion)
    {
        if (_automationEnabled || _startRequested)
        {
            RodNameRecognitionStatusText.Text =
                $"“{recognizedName}” matches profile “{profileName}”, but profile switching is deferred until the macro stops.";
            return;
        }

        var profilePath = GetProfilePath(profileName);
        if (!File.Exists(profilePath))
        {
            RodNameRecognitionStatusText.Text =
                $"Profile “{profileName}” for “{recognizedName}” is missing. Rebind the rod name after saving that profile.";
            return;
        }

        _isLoadingBoundRodProfile = true;
        try
        {
            var store = CreateProfileStore(profileName);
            var profile = await store.LoadOrDefaultAsync();
            if (selectionVersion != Volatile.Read(ref _rodProfileSelectionVersion)
                || _automationEnabled || _startRequested)
                return;

            _profileStore = store;
            _profile = profile;
            _activeProfileName = profileName;
            RodProfileNameTextBox.Text = profileName;
            _rodProfileBindingError = false;
            _motionTracker.Reset();
            _castVision.Reset();
            RefreshRodReelPresetControls();
            RefreshRodProfileBindingStatus();
            RodNameRecognitionStatusText.Text =
                $"Rod “{recognizedName}” confirmed. Loaded calibration profile “{profileName}”.";
            TelemetryText.Text = $"Profile: {profileName} · selected by rod-name OCR.";
            AddActivityLog("INFO", "PROFILE", $"Rod OCR selected profile '{profileName}' for '{recognizedName}'.");
        }
        catch (Exception exception)
        {
            RodNameRecognitionStatusText.Text = $"Could not load profile “{profileName}”: {exception.Message}";
        }
        finally
        {
            _isLoadingBoundRodProfile = false;
            RefreshRodProfileBindingStatus();
        }
    }

    private async Task<LocalTesseractOcr> EnsureLocalOcrEngineAsync()
    {
        await _ocrInitializationGate.WaitAsync();
        try
        {
            if (_ocr is not null)
                return _ocr;

            var tessdataDirectory = await TesseractModelInstaller.EnsureEnglishModelAsync();
            _ocr = await Task.Run(() => new LocalTesseractOcr(tessdataDirectory));
            return _ocr;
        }
        finally
        {
            _ocrInitializationGate.Release();
        }
    }

    private async void BindRodProfileToSlot_Click(object sender, RoutedEventArgs e)
    {
        if (_automationEnabled || _startRequested || _isLoadingBoundRodProfile)
        {
            RodProfileBindingStatusText.Text = "Stop the macro and wait for profile loading before changing rod bindings.";
            return;
        }

        if (EquipRodCheckBox.IsChecked != true)
        {
            RodProfileBindingStatusText.Text = "Enable “Equip my rod” first, then choose the hotbar slot.";
            return;
        }

        try
        {
            var slot = ReadHotbarSlot(RodSlotComboBox, "rod");
            var profileName = RodProfileNameTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(profileName)
                || !string.Equals(profileName, _activeProfileName, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Load or save the profile shown as active before binding it to a rod slot.");
            }

            var profileStore = CreateProfileStore(profileName);
            await profileStore.SaveAsync(_profile);

            var updated = new RodProfileSlotBindings
            {
                SlotProfiles = new Dictionary<int, string>(_rodProfileBindings.SlotProfiles)
                {
                    [slot] = profileName
                },
                RodNameProfiles = new Dictionary<string, string>(
                    _rodProfileBindings.RodNameProfiles, StringComparer.OrdinalIgnoreCase)
            };
            await _rodProfileBindingStore.SaveAsync(updated);
            _rodProfileBindings = updated;
            _profileStore = profileStore;
            _rodProfileBindingError = false;
            RefreshRodProfileBindingStatus();
            AddActivityLog("INFO", "PROFILE", $"Profile '{profileName}' bound to rod hotbar slot {slot}.");
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        {
            RodProfileBindingStatusText.Text = $"Could not bind profile: {exception.Message}";
        }
    }

    private void RodSlotComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var requestVersion = Interlocked.Increment(ref _rodProfileSelectionVersion);
        RefreshRodProfileBindingStatus();
        _ = LoadBoundRodProfileForSelectedSlotAsync(requestVersion);
    }

    private async Task LoadBoundRodProfileForSelectedSlotAsync(int requestVersion)
    {
        bool IsCurrentRequest() =>
            requestVersion == Volatile.Read(ref _rodProfileSelectionVersion);

        if (!IsLoaded || _automationEnabled || _startRequested
            || EquipRodCheckBox.IsChecked != true)
        {
            if (IsCurrentRequest())
            {
                _isLoadingBoundRodProfile = false;
                if (EquipRodCheckBox.IsChecked != true)
                    _rodProfileBindingError = false;
            }
            return;
        }

        int slot;
        try
        {
            slot = ReadHotbarSlot(RodSlotComboBox, "rod");
        }
        catch (InvalidOperationException)
        {
            if (IsCurrentRequest())
                _isLoadingBoundRodProfile = false;
            return;
        }

        if (!_rodProfileBindings.SlotProfiles.TryGetValue(slot, out var profileName))
        {
            if (IsCurrentRequest())
            {
                _isLoadingBoundRodProfile = false;
                _rodProfileBindingError = false;
                RefreshRodProfileBindingStatus();
            }
            return;
        }

        if (string.Equals(profileName, _activeProfileName, StringComparison.OrdinalIgnoreCase))
        {
            if (IsCurrentRequest())
            {
                _isLoadingBoundRodProfile = false;
                _rodProfileBindingError = false;
                RefreshRodProfileBindingStatus();
            }
            return;
        }

        _isLoadingBoundRodProfile = true;
        try
        {
            var path = GetProfilePath(profileName);
            if (!File.Exists(path))
                throw new FileNotFoundException(
                    $"The profile '{profileName}' bound to slot {slot} no longer exists. Rebind this slot after saving the profile.",
                    path);

            if (_connectionRecoveryStartRequested || _connectionRecoveryCoordinator is not null)
                StopConnectionRecovery("Rod profile changed; reconnect watchdog was disarmed.");

            var profileStore = CreateProfileStore(profileName);
            var profile = await profileStore.LoadOrDefaultAsync();

            // A newer combo selection, manual profile action or emergency stop
            // invalidates this asynchronous load. Never apply stale calibration.
            if (!IsCurrentRequest()
                || EquipRodCheckBox.IsChecked != true
                || _automationEnabled
                || _startRequested
                || ReadHotbarSlot(RodSlotComboBox, "rod") != slot
                || Volatile.Read(ref _emergencyStopLatch) != 0)
                return;

            _profileStore = profileStore;
            _profile = profile;
            _activeProfileName = profileName;
            RefreshRodReelPresetControls();
            _rodProfileBindingError = false;
            RodProfileNameTextBox.Text = profileName;
            _motionTracker.Reset();
            _castVision.Reset();
            _huntDetectionCoordinator.Reset();

            CalibrationModeText.Text = $"Automatically loaded profile '{profileName}' for rod slot {slot}.";
            VisionStatusText.Text = _profile.IsFishingCalibrated
                ? "Vision: rod profile calibration loaded."
                : "Vision: rod profile needs fishing calibration.";
            CastStatusText.Text = _profile.IsCastCalibrated
                ? "Perfect Cast: rod profile meter calibration loaded."
                : "Perfect Cast: awaiting meter calibration.";
            ShakeStatusText.Text = _profile.IsShakeCalibrated
                ? "Shake: rod profile prompt calibration loaded."
                : "Shake: awaiting prompt calibration.";
            SellStatusText.Text = _profile.IsSellButtonCalibrated
                ? "Sell Anywhere: rod profile button calibration loaded."
                : "Sell Anywhere: awaiting button calibration.";
            ConnectionRecoveryStatusText.Text = _profile.IsReconnectButtonCalibrated
                ? "Reconnect button calibration loaded."
                : "Reconnect button calibration is missing.";
            WorldPhaseCalibrationStatusText.Text = _profile.IsWorldPhaseCalibrated
                ? "Day/night phase calibration loaded."
                : "Calibrate the phase region and sample both colours.";
            DayNightTotemStatusText.Text = _profile.IsWorldPhaseCalibrated
                ? "Phase calibration loaded. The first stable phase will set a baseline."
                : "Day/night totems need phase calibration.";
            HuntDetectionStatusText.Text = _profile.IsHuntSignalCalibrated
                ? "Hunt cue calibration loaded."
                : "Calibrate the Hunt cue before enabling alerts.";
            OcrStatusText.Text = _profile.IsCatchNotificationCalibrated
                ? "Local OCR: catch notification region loaded."
                : "Local OCR: select the catch notification region before starting a session.";
            UpdateConnectionRecoveryControls();
            RefreshModuleQuickInfo();
            TelemetryText.Text = $"Profile: {_activeProfileName} · auto-selected for slot {slot}.";
            AddActivityLog("INFO", "PROFILE", $"Automatically loaded profile '{profileName}' for rod hotbar slot {slot}.");
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        {
            if (IsCurrentRequest())
            {
                _rodProfileBindingError = true;
                RodProfileBindingStatusText.Text = $"Could not load profile for slot {slot}: {exception.Message}";
                AddActivityLog("ERROR", "PROFILE", $"Automatic rod profile selection failed for slot {slot}: {exception.Message}");
            }
        }
        finally
        {
            if (IsCurrentRequest())
            {
                _isLoadingBoundRodProfile = false;
                RefreshRodProfileBindingStatus();
            }
        }
    }

    private async void ClearRodProfileBinding_Click(object sender, RoutedEventArgs e)
    {
        if (_automationEnabled || _startRequested || _isLoadingBoundRodProfile)
        {
            RodProfileBindingStatusText.Text = "Stop the macro and wait for profile loading before changing rod bindings.";
            return;
        }

        if (EquipRodCheckBox.IsChecked != true)
        {
            RodProfileBindingStatusText.Text = "Enable “Equip my rod” first, then choose the hotbar slot.";
            return;
        }

        try
        {
            var slot = ReadHotbarSlot(RodSlotComboBox, "rod");
            var updatedSlots = new Dictionary<int, string>(_rodProfileBindings.SlotProfiles);
            if (!updatedSlots.Remove(slot))
            {
                RodProfileBindingStatusText.Text = $"Hotbar slot {slot} has no saved profile binding.";
                return;
            }

            var updated = new RodProfileSlotBindings
            {
                SlotProfiles = updatedSlots,
                RodNameProfiles = new Dictionary<string, string>(
                    _rodProfileBindings.RodNameProfiles, StringComparer.OrdinalIgnoreCase)
            };
            await _rodProfileBindingStore.SaveAsync(updated);
            _rodProfileBindings = updated;
            _rodProfileBindingError = false;
            RefreshRodProfileBindingStatus();
            AddActivityLog("INFO", "PROFILE", $"Removed calibration profile binding from rod hotbar slot {slot}.");
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        {
            RodProfileBindingStatusText.Text = $"Could not clear profile binding: {exception.Message}";
        }
    }

    private async void ExportRodProfile_Click(object sender, RoutedEventArgs e)
    {
        if (_automationEnabled)
        {
            CalibrationModeText.Text = "Stop the macro before exporting a calibration profile.";
            return;
        }

        var profileName = RodProfileNameTextBox.Text.Trim();
        var safeName = string.IsNullOrWhiteSpace(profileName) ? "Default" : profileName;
        var dialog = new SaveFileDialog
        {
            Title = "Export GafiFish calibration profile",
            Filter = "GafiFish calibration profile (*.json)|*.json",
            DefaultExt = "json",
            AddExtension = true,
            FileName = $"GafiFish-{ProfileSlug(safeName)}-profile.json",
            OverwritePrompt = true
        };
        if (dialog.ShowDialog(this) != true)
            return;

        try
        {
            await new VisionProfileStore(dialog.FileName).SaveAsync(_profile);
            CalibrationModeText.Text = $"Profile exported to {System.IO.Path.GetFileName(dialog.FileName)}.";
        }
        catch (Exception exception)
        {
            CalibrationModeText.Text = $"Could not export profile: {exception.Message}";
        }
    }

    private async void ImportRodProfile_Click(object sender, RoutedEventArgs e)
    {
        Interlocked.Increment(ref _rodProfileSelectionVersion);
        _isLoadingBoundRodProfile = false;
        if (_automationEnabled || _startRequested)
        {
            CalibrationModeText.Text = "Stop the macro before importing a calibration profile.";
            return;
        }
        if (_connectionRecoveryStartRequested || _connectionRecoveryCoordinator is not null)
            StopConnectionRecovery("Profile import requested; reconnect watchdog was disarmed.");

        var dialog = new OpenFileDialog
        {
            Title = "Import GafiFish calibration profile",
            Filter = "GafiFish calibration profile (*.json)|*.json",
            CheckFileExists = true,
            Multiselect = false
        };
        if (dialog.ShowDialog(this) != true)
            return;

        try
        {
            var importedProfile = await new VisionProfileStore(dialog.FileName).LoadOrDefaultAsync();
            var hasCalibration = importedProfile.IsFishingCalibrated
                || importedProfile.IsCastCalibrated
                || importedProfile.IsShakeCalibrated
                || importedProfile.IsSellButtonCalibrated
                || importedProfile.IsInventoryVerificationCalibrated
                || importedProfile.IsCatchNotificationCalibrated
                || importedProfile.IsReconnectButtonCalibrated
                || importedProfile.IsWorldPhaseCalibrated
                || importedProfile.IsAnglerAcceptButtonCalibrated
                || importedProfile.IsAnglerSubmitButtonCalibrated
                || importedProfile.RodNameRegionCalibrated
;
            if (!hasCalibration)
                throw new InvalidDataException("The selected JSON does not contain a usable GafiFish calibration profile.");

            // This manual import takes precedence over any slot selection that
            // started while the file was being read.
            Interlocked.Increment(ref _rodProfileSelectionVersion);
            _isLoadingBoundRodProfile = false;

            var profileName = RodProfileNameTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(profileName))
                profileName = System.IO.Path.GetFileNameWithoutExtension(dialog.FileName);

            _activeProfileName = profileName;
            _profileStore = CreateProfileStore(_activeProfileName);
            _profile = importedProfile;
            RefreshRodReelPresetControls();
            await _profileStore.SaveAsync(_profile);
            _rodProfileBindingError = false;
            RodProfileNameTextBox.Text = _activeProfileName;

            _motionTracker.Reset();
            _castVision.Reset();
            CalibrationModeText.Text = $"Imported and saved profile '{_activeProfileName}'.";
            VisionStatusText.Text = _profile.IsFishingCalibrated ? "Vision: profile calibration loaded." : "Vision: profile needs fishing calibration.";
            CastStatusText.Text = _profile.IsCastCalibrated ? "Perfect Cast: profile calibration loaded." : "Perfect Cast: awaiting meter calibration.";
            ShakeStatusText.Text = _profile.IsShakeCalibrated ? "Shake: profile calibration loaded." : "Shake: awaiting prompt calibration.";
            SellStatusText.Text = _profile.IsSellButtonCalibrated ? "Sell Anywhere: profile calibration loaded." : "Sell Anywhere: awaiting button calibration.";
            ConnectionRecoveryStatusText.Text = _profile.IsReconnectButtonCalibrated ? "Reconnect button calibration imported." : "Reconnect button calibration is missing.";
            WorldPhaseCalibrationStatusText.Text = _profile.IsWorldPhaseCalibrated ? "Day/night phase calibration imported." : "Day/night phase calibration is missing.";
            DayNightTotemStatusText.Text = _profile.IsWorldPhaseCalibrated ? "Phase calibration imported." : "Calibrate both day and night colours.";
            OcrStatusText.Text = _profile.IsCatchNotificationCalibrated ? "Local OCR: catch notification region loaded." : "Local OCR: select the catch notification region before starting a session.";
            UpdateConnectionRecoveryControls();
            TelemetryText.Text = $"Profile: {_activeProfileName} · imported.";
        }
        catch (Exception exception)
        {
            CalibrationModeText.Text = $"Could not import profile: {exception.Message}";
        }
    }

    private async void SaveRodProfile_Click(object sender, RoutedEventArgs e)
    {
        if (_automationEnabled || _startRequested || _isLoadingBoundRodProfile)
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
            RefreshRodProfileBindingStatus();
        }
        catch (Exception exception)
        {
            CalibrationModeText.Text = $"Could not save profile: {exception.Message}";
        }
    }

    private async void LoadRodProfile_Click(object sender, RoutedEventArgs e)
    {
        Interlocked.Increment(ref _rodProfileSelectionVersion);
        _isLoadingBoundRodProfile = false;
        if (_automationEnabled || _startRequested)
        {
            CalibrationModeText.Text = "Stop the macro before loading a profile.";
            return;
        }
        if (_connectionRecoveryStartRequested || _connectionRecoveryCoordinator is not null)
            StopConnectionRecovery("Profile change requested; reconnect watchdog was disarmed.");
        try
        {
            var requestedProfileName = RodProfileNameTextBox.Text.Trim();
            var requestedProfileStore = CreateProfileStore(requestedProfileName);
            var requestedProfile = await requestedProfileStore.LoadOrDefaultAsync();

            // The explicit Load profile action wins over any automatic slot
            // load that may have started while this file was being read.
            Interlocked.Increment(ref _rodProfileSelectionVersion);
            _isLoadingBoundRodProfile = false;
            _activeProfileName = requestedProfileName;
            _profileStore = requestedProfileStore;
            _profile = requestedProfile;
            RefreshRodReelPresetControls();
            _rodProfileBindingError = false;
            _motionTracker.Reset();
            _castVision.Reset();
            CalibrationModeText.Text = $"Loaded profile '{_activeProfileName}'.";
            VisionStatusText.Text = _profile.IsFishingCalibrated ? "Vision: profile calibration loaded." : "Vision: profile needs fishing calibration.";
            CastStatusText.Text = _profile.IsCastCalibrated ? "Perfect Cast: profile calibration loaded." : "Perfect Cast: awaiting meter calibration.";
            ShakeStatusText.Text = _profile.IsShakeCalibrated ? "Shake: profile calibration loaded." : "Shake: awaiting prompt calibration.";
            SellStatusText.Text = _profile.IsSellButtonCalibrated ? "Sell Anywhere: profile calibration loaded." : "Sell Anywhere: awaiting button calibration.";
            ConnectionRecoveryStatusText.Text = _profile.IsReconnectButtonCalibrated ? "Reconnect button calibration loaded." : "Reconnect button calibration is missing.";
            WorldPhaseCalibrationStatusText.Text = _profile.IsWorldPhaseCalibrated ? "Day/night phase calibration loaded." : "Day/night phase calibration is missing.";
            DayNightTotemStatusText.Text = _profile.IsWorldPhaseCalibrated ? "Phase calibration loaded." : "Calibrate both day and night colours.";
            OcrStatusText.Text = _profile.IsCatchNotificationCalibrated ? "Local OCR: catch notification region loaded." : "Local OCR: select the catch notification region before starting a session.";
            UpdateConnectionRecoveryControls();
            TelemetryText.Text = $"Profile: {_activeProfileName} · loaded.";
            RefreshRodProfileBindingStatus();
            AddActivityLog("INFO", "PROFILE", $"Calibration profile '{_activeProfileName}' loaded.");
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

    private void SelectReconnectButtonRegion_Click(object sender, RoutedEventArgs e)
    {
        SetCalibrationAction(CalibrationAction.SelectReconnectButtonRegion,
            "Drag a tight rectangle around the Roblox reconnect button while it is visible.");
    }

    private void SampleReconnectButtonColor_Click(object sender, RoutedEventArgs e)
    {
        SetCalibrationAction(CalibrationAction.SampleReconnectButtonColor,
            "Click a clear pixel inside the reconnect button. Use the same Roblox UI theme and resolution.");
    }

    private void CaptureFishMarkerTemplate_Click(object sender, RoutedEventArgs e)
    {
        SetCalibrationAction(CalibrationAction.CaptureFishMarkerTemplate,
            "Draw a tight rectangle around the fish marker only. Use a frame where it is clear and not overlapping the control bar.");
    }

    private void CaptureControlMarkerTemplate_Click(object sender, RoutedEventArgs e)
    {
        SetCalibrationAction(CalibrationAction.CaptureControlMarkerTemplate,
            "Draw a tight rectangle around the control marker only. Avoid including the fish marker or background.");
    }

    private void SelectWorldPhaseRegion_Click(object sender, RoutedEventArgs e)
    {
        SetCalibrationAction(CalibrationAction.SelectWorldPhaseRegion,
            "Select a small, fixed HUD indicator that changes between day and night. Avoid the sky or animated scenery.");
    }

    private void SampleDayPhaseColor_Click(object sender, RoutedEventArgs e)
    {
        SetCalibrationAction(CalibrationAction.SampleDayPhaseColor,
            "Click a clear pixel inside the indicator while the world is in its DAY phase.");
    }

    private void SampleNightPhaseColor_Click(object sender, RoutedEventArgs e)
    {
        SetCalibrationAction(CalibrationAction.SampleNightPhaseColor,
            "Click a clear pixel inside the same indicator while the world is in its NIGHT phase.");
    }

    private void SelectHuntSignalRegion_Click(object sender, RoutedEventArgs e)
    {
        SetCalibrationAction(CalibrationAction.SelectHuntSignalRegion,
            "Drag a tight rectangle around the visible hunt announcement or HUD cue. Calibrate it while the cue is on screen.");
    }

    private void SampleHuntSignalColor_Click(object sender, RoutedEventArgs e)
    {
        SetCalibrationAction(CalibrationAction.SampleHuntSignalColor,
            "Click a clear, solid-colour pixel inside the hunt announcement or HUD cue.");
    }

    private void HuntDetectionEnabledCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        _huntDetectionCoordinator.Reset();
        HuntDetectionStatusText.Text = HuntDetectionEnabledCheckBox.IsChecked == true
            ? _profile.IsHuntSignalCalibrated
                ? "Hunt alerts enabled. Start live preview to monitor the calibrated cue."
                : "Hunt alerts enabled, but the cue region and colour still need calibration."
            : "Hunt alerts are disabled.";
        RefreshModuleQuickInfo();
    }

    private void EquipRodCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (!_automationEnabled)
        {
            var requestVersion = Interlocked.Increment(ref _rodProfileSelectionVersion);
            RodSlotComboBox.IsEnabled = EquipRodCheckBox.IsChecked == true;
            if (EquipRodCheckBox.IsChecked == true)
                _ = LoadBoundRodProfileForSelectedSlotAsync(requestVersion);
            else
            {
                _isLoadingBoundRodProfile = false;
                _rodProfileBindingError = false;
            }

            RefreshRodProfileBindingStatus();
        }
        RefreshModuleQuickInfo();
    }

    private void SetCalibrationAction(CalibrationAction action, string instruction)
    {
        if (_automationEnabled)
            StopAutomation("Calibration changed; Reel assist was disarmed and all inputs released.");
        if (_aquariumStartRequested || _aquariumAutomationCoordinator is not null)
            StopAquariumAutomation("Calibration changed; Auto Aquarium was disarmed.");
        if (_connectionRecoveryStartRequested || _connectionRecoveryCoordinator is not null)
            StopConnectionRecovery("Calibration changed; connection watchdog was disarmed.");
        if (_anglerQuestStartRequested || _anglerQuestAutomationCoordinator is not null)
            StopAnglerQuestAutomation("Calibration changed; Angler automation was disarmed.");

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
            or CalibrationAction.SampleSellButton
            or CalibrationAction.SampleReconnectButtonColor
            or CalibrationAction.SampleDayPhaseColor
            or CalibrationAction.SampleNightPhaseColor
            or CalibrationAction.SampleHuntSignalColor
            or CalibrationAction.SampleAquariumButtonColor
            or CalibrationAction.SampleAnglerAcceptButtonColor
            or CalibrationAction.SampleAnglerSubmitButtonColor
            or CalibrationAction.SampleRodChargeMeterColor)
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
            or CalibrationAction.SelectCatchNotificationRegion
            or CalibrationAction.SelectReconnectButtonRegion
            or CalibrationAction.SelectWorldPhaseRegion
            or CalibrationAction.SelectHuntSignalRegion
            or CalibrationAction.CaptureFishMarkerTemplate
            or CalibrationAction.CaptureControlMarkerTemplate
            or CalibrationAction.SelectAquariumButtonRegion
            or CalibrationAction.SelectAnglerAcceptButtonRegion
            or CalibrationAction.SelectAnglerSubmitButtonRegion
            or CalibrationAction.SelectRodNameRegion
            or CalibrationAction.SelectRodChargeMeterRegion)
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
            or CalibrationAction.SelectCatchNotificationRegion
            or CalibrationAction.SelectReconnectButtonRegion
            or CalibrationAction.SelectWorldPhaseRegion
            or CalibrationAction.SelectHuntSignalRegion
            or CalibrationAction.CaptureFishMarkerTemplate
            or CalibrationAction.CaptureControlMarkerTemplate
            or CalibrationAction.SelectAquariumButtonRegion
            or CalibrationAction.SelectAnglerAcceptButtonRegion
            or CalibrationAction.SelectAnglerSubmitButtonRegion
            or CalibrationAction.SelectRodNameRegion
            or CalibrationAction.SelectRodChargeMeterRegion)
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
            or CalibrationAction.SelectCatchNotificationRegion
            or CalibrationAction.SelectReconnectButtonRegion
            or CalibrationAction.SelectWorldPhaseRegion
            or CalibrationAction.SelectHuntSignalRegion
            or CalibrationAction.CaptureFishMarkerTemplate
            or CalibrationAction.CaptureControlMarkerTemplate
            or CalibrationAction.SelectAquariumButtonRegion
            or CalibrationAction.SelectAnglerAcceptButtonRegion
            or CalibrationAction.SelectAnglerSubmitButtonRegion
            or CalibrationAction.SelectRodNameRegion
            or CalibrationAction.SelectRodChargeMeterRegion)
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
        if ((selectedAction is CalibrationAction.CaptureFishMarkerTemplate
            or CalibrationAction.CaptureControlMarkerTemplate)
            && (width > 128 || height > 128))
        {
            CalibrationModeText.Text = "Marker templates are limited to 128×128 pixels. Draw a tighter box around the marker.";
            return;
        }

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
            CalibrationAction.SelectReconnectButtonRegion => _profile with
            {
                ReconnectButtonRegion = selectedRegion,
                ReconnectButtonRegionCalibrated = true,
                ReferenceWidth = _lastFrame.Width,
                ReferenceHeight = _lastFrame.Height
            },
            CalibrationAction.SelectWorldPhaseRegion => _profile with
            {
                WorldPhaseRegion = selectedRegion,
                WorldPhaseRegionCalibrated = true,
                ReferenceWidth = _lastFrame.Width,
                ReferenceHeight = _lastFrame.Height
            },
            CalibrationAction.SelectHuntSignalRegion => _profile with
            {
                HuntSignalRegion = selectedRegion,
                HuntSignalRegionCalibrated = true,
                ReferenceWidth = _lastFrame.Width,
                ReferenceHeight = _lastFrame.Height
            },
            CalibrationAction.CaptureFishMarkerTemplate => _profile with
            {
                FishMarkerTemplatePng = EncodeMarkerTemplate(_lastFrame, selectedRegion),
                ReferenceWidth = _lastFrame.Width,
                ReferenceHeight = _lastFrame.Height
            },
            CalibrationAction.CaptureControlMarkerTemplate => _profile with
            {
                ControlMarkerTemplatePng = EncodeMarkerTemplate(_lastFrame, selectedRegion),
                ReferenceWidth = _lastFrame.Width,
                ReferenceHeight = _lastFrame.Height
            },
            CalibrationAction.SelectAquariumButtonRegion => WithAquariumButtonRegion(
                _profile, _aquariumCalibrationActionKind, selectedRegion, _lastFrame.Width, _lastFrame.Height),
            CalibrationAction.SelectAnglerAcceptButtonRegion => WithAnglerQuestButtonRegion(
                _profile, AnglerQuestButtonKind.AcceptRequest, selectedRegion, _lastFrame.Width, _lastFrame.Height),
            CalibrationAction.SelectAnglerSubmitButtonRegion => WithAnglerQuestButtonRegion(
                _profile, AnglerQuestButtonKind.SubmitFish, selectedRegion, _lastFrame.Width, _lastFrame.Height),
            CalibrationAction.SelectRodNameRegion => _profile with
            {
                RodNameRegion = selectedRegion,
                RodNameRegionCalibrated = true,
                ReferenceWidth = _lastFrame.Width,
                ReferenceHeight = _lastFrame.Height
            },
            CalibrationAction.SelectRodChargeMeterRegion => _profile with
            {
                RodChargeMeterRegion = selectedRegion,
                RodChargeMeterRegionCalibrated = true,
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
            CalibrationAction.SelectReconnectButtonRegion => "Reconnect button region saved.",
            CalibrationAction.SelectWorldPhaseRegion => "Day/night phase indicator region saved.",
            CalibrationAction.SelectHuntSignalRegion => "Hunt cue region saved.",
            CalibrationAction.CaptureFishMarkerTemplate => "Fish marker template captured. HSV remains preferred when confidence is sufficient.",
            CalibrationAction.CaptureControlMarkerTemplate => "Control marker template captured. HSV remains preferred when confidence is sufficient.",
            CalibrationAction.SelectAquariumButtonRegion => $"{AquariumAutomationCoordinator.FormatAction(_aquariumCalibrationActionKind)} button area saved.",
            CalibrationAction.SelectAnglerAcceptButtonRegion => "Angler Accept Request button area saved.",
            CalibrationAction.SelectAnglerSubmitButtonRegion => "Angler Submit Fish button area saved.",
            CalibrationAction.SelectRodNameRegion => "Rod-name OCR region saved.",
            CalibrationAction.SelectRodChargeMeterRegion => "Rod charge-meter region saved.",
            _ => "Region saved."
        };
        await SaveProfileAsync(regionMessage);
        _calibrationAction = CalibrationAction.None;
        e.Handled = true;
    }

    private static byte[] EncodeMarkerTemplate(ScreenCaptureFrame frame, NormalizedRegion region)
    {
        var rect = region.ToPixelRect(frame.Width, frame.Height);
        using var bgra = new Mat(frame.Height, frame.Width, MatType.CV_8UC4);
        Marshal.Copy(frame.BgraPixels, 0, bgra.Data, frame.BgraPixels.Length);
        using var crop = new Mat(bgra, rect);
        using var bgr = new Mat();
        Cv2.CvtColor(crop, bgr, ColorConversionCodes.BGRA2BGR);
        using var gray = new Mat();
        Cv2.CvtColor(bgr, gray, ColorConversionCodes.BGR2GRAY);
        if (!Cv2.ImEncode(".png", gray, out var encoded))
            throw new InvalidOperationException("OpenCV could not encode the selected marker template.");
        return encoded;
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

        var isReconnectColor = _calibrationAction == CalibrationAction.SampleReconnectButtonColor;
        var isDayPhaseColor = _calibrationAction == CalibrationAction.SampleDayPhaseColor;
        var isNightPhaseColor = _calibrationAction == CalibrationAction.SampleNightPhaseColor;
        var isHuntSignalColor = _calibrationAction == CalibrationAction.SampleHuntSignalColor;
        var isAnglerButtonColor = _calibrationAction is CalibrationAction.SampleAnglerAcceptButtonColor
            or CalibrationAction.SampleAnglerSubmitButtonColor;
        var isRodChargeMeterColor = _calibrationAction == CalibrationAction.SampleRodChargeMeterColor;
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
            CalibrationAction.SampleReconnectButtonColor => _profile with { ReconnectButtonColor = color },
            CalibrationAction.SampleDayPhaseColor => _profile with { DayPhaseColor = color },
            CalibrationAction.SampleNightPhaseColor => _profile with { NightPhaseColor = color },
            CalibrationAction.SampleHuntSignalColor => _profile with { HuntSignalColor = color },
            CalibrationAction.SampleAquariumButtonColor => WithAquariumButtonColor(
                _profile, _aquariumCalibrationActionKind, color, _lastFrame.Width, _lastFrame.Height),
            CalibrationAction.SampleAnglerAcceptButtonColor => WithAnglerQuestButtonColor(
                _profile, AnglerQuestButtonKind.AcceptRequest, color, _lastFrame.Width, _lastFrame.Height),
            CalibrationAction.SampleAnglerSubmitButtonColor => WithAnglerQuestButtonColor(
                _profile, AnglerQuestButtonKind.SubmitFish, color, _lastFrame.Width, _lastFrame.Height),
            CalibrationAction.SampleRodChargeMeterColor => _profile with { RodChargeMeterColor = color },
            _ => _profile
        };

        _profile = _profile with
        {
            ReferenceWidth = _lastFrame.Width,
            ReferenceHeight = _lastFrame.Height
        };

        _calibrationAction = CalibrationAction.None;
        _motionTracker.Reset();
        if (isReconnectColor)
        {
            ConnectionRecoveryStatusText.Text = "Reconnect button colour saved. The watchdog is ready to be armed.";
            UpdateConnectionRecoveryControls();
        }
        if (isHuntSignalColor)
        {
            HuntDetectionStatusText.Text = _profile.IsHuntSignalCalibrated
                ? "Hunt cue calibration complete. Enable alerts to monitor live frames."
                : "Hunt cue colour saved. Select the cue region to complete calibration.";
        }
        if (isAnglerButtonColor)
            AnglerQuestAutomationStatusText.Text = _profile.IsAnglerAcceptButtonCalibrated
                && _profile.IsAnglerSubmitButtonCalibrated
                ? "Both Angler dialogue buttons are calibrated."
                : "Angler button colour saved. Complete the remaining button calibration.";
        if (isRodChargeMeterColor)
        {
            RodMinigameModeStatusText.Text = _profile.IsRodChargeMeterCalibrated
                ? "Charge Meter Bonus · calibration complete. It will release at 80% when the fish is inside the bar."
                : "Charge fill colour saved. Select the full meter track to finish calibration.";
            RodChargeMeterStatusText.Text = "Charge meter calibration updated.";
        }

        if (isDayPhaseColor || isNightPhaseColor)
        {
            WorldPhaseCalibrationStatusText.Text = _profile.IsWorldPhaseCalibrated
                ? "Day/night phase calibration complete."
                : isDayPhaseColor
                    ? "Day colour saved. Capture a night frame and sample its indicator colour."
                    : "Night colour saved. Capture a day frame and sample its indicator colour.";
            DayNightTotemStatusText.Text = _profile.IsWorldPhaseCalibrated
                ? "Phase calibration complete. Day/night rotation is ready."
                : "Both day and night colours are required before arming.";
            UpdateTimedItemControls();
        }
        await SaveProfileAsync("Marker color saved.");
    }

    private async void ResetCalibration_Click(object sender, RoutedEventArgs e)
    {
        if (_automationEnabled)
            StopAutomation("Calibration reset; Reel assist was disarmed and all inputs released.");
        if (_aquariumStartRequested || _aquariumAutomationCoordinator is not null)
            StopAquariumAutomation("Calibration reset; Auto Aquarium was disarmed.");
        if (_connectionRecoveryStartRequested || _connectionRecoveryCoordinator is not null)
            StopConnectionRecovery("Calibration reset; connection watchdog was disarmed.");
        if (_anglerQuestStartRequested || _anglerQuestAutomationCoordinator is not null)
            StopAnglerQuestAutomation("Calibration reset; Angler automation was disarmed.");

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
        ConnectionRecoveryStatusText.Text = "Calibrate the reconnect button region and colour before arming the watchdog.";
        WorldPhaseCalibrationStatusText.Text = "Calibrate the phase region and sample the day and night colours.";
        HuntDetectionStatusText.Text = "Hunt cue calibration was reset.";
        DayNightTotemStatusText.Text = "Day/night totems need phase calibration.";
        OcrStatusText.Text = "Local OCR: select the catch notification region before starting a session.";
        UpdateConnectionRecoveryControls();
    }

    private async Task SaveProfileAsync(string successMessage)
    {
        try
        {
            await _profileStore.SaveAsync(_profile);

            // Calibration changes invalidate temporal estimates. Never carry
            // fish velocity or cast-indicator history across profile edits.
            _motionTracker.Reset();
            _castVision.Reset();
            _huntDetectionCoordinator.Reset();

            UpdateConnectionRecoveryControls();
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
            await EnsureLocalOcrEngineAsync();
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
            SessionStatusText.Text = "Session running. Start observation to detect catch notifications.";
            SessionSummaryText.Text = string.Empty;
            AddActivityLog("INFO", "SESSION", $"Local OCR session started ({sessionId:D}).");
            RefreshModuleQuickInfo();
            await RefreshLifetimeStatisticsAsync();
            await RefreshSessionHistoryAsync(sessionId);
            await RefreshDashboardSessionAsync();
        }
        catch (Exception exception)
        {
            OcrStatusText.Text = $"Local OCR could not start: {exception.Message}";
            SessionStatusText.Text = "No session started. Check the network connection and the Microsoft Visual C++ 2022 runtime.";
            AddActivityLog("ERROR", "SESSION", $"Could not start local OCR session: {exception.Message}");
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
            await RefreshDashboardSessionAsync();
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
            AddActivityLog("INFO", "SESSION",
                $"Session completed · {summary.TotalCatches} catches · {summary.Duration.TotalMinutes:F1} minutes.");

            OcrStatusText.Text = "Local OCR remains ready for the next session.";
            RefreshModuleQuickInfo();
            StartSessionButton.IsEnabled = true;
            await RefreshLifetimeStatisticsAsync();
            await RefreshSessionHistoryAsync(sessionId);
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

    private static string FormatBackupSize(long bytes) =>
        bytes < 1024 ? $"{bytes} B"
        : bytes < 1024 * 1024 ? $"{bytes / 1024d:F1} KB"
        : $"{bytes / 1024d / 1024d:F1} MB";

    private async void CreateDataBackup_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Title = "Create GafiFish data backup",
            Filter = "GafiFish backup (*.zip)|*.zip",
            DefaultExt = "zip",
            AddExtension = true,
            FileName = $"GafiFish-backup-{DateTime.Now:yyyyMMdd-HHmmss}.zip",
            OverwritePrompt = true
        };
        if (dialog.ShowDialog(this) != true)
            return;

        CreateDataBackupButton.IsEnabled = false;
        VerifyDataBackupButton.IsEnabled = false;
        DataBackupStatusText.Text = "Creating a consistent local data backup…";

        try
        {
            var manifest = await _userDataBackupService.CreateBackupAsync(dialog.FileName);
            var sizeBytes = new FileInfo(dialog.FileName).Length;
            DataBackupStatusText.Text =
                $"Backup created · {manifest.Files.Count:N0} files · {sizeBytes / 1024d / 1024d:F1} MB · {DateTime.Now:dd/MM/yyyy HH:mm:ss}";
            AddActivityLog("INFO", "BACKUP",
                $"Created local data backup with {manifest.Files.Count} files ({sizeBytes:N0} bytes).");
        }
        catch (Exception exception)
        {
            DataBackupStatusText.Text = $"Backup failed: {exception.Message}";
            AddActivityLog("ERROR", "BACKUP", $"Could not create backup: {exception.Message}");
        }
        finally
        {
            CreateDataBackupButton.IsEnabled = true;
            VerifyDataBackupButton.IsEnabled = true;
        }
    }

    private async void VerifyDataBackup_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Verify a GafiFish data backup",
            Filter = "GafiFish backup (*.zip)|*.zip|All files (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false
        };
        if (dialog.ShowDialog(this) != true)
            return;

        CreateDataBackupButton.IsEnabled = false;
        VerifyDataBackupButton.IsEnabled = false;
        DataBackupStatusText.Text = "Checking the backup manifest and SHA-256 hashes…";

        try
        {
            var result = await _userDataBackupService.VerifyBackupAsync(dialog.FileName);
            DataBackupStatusText.Text = result.Message;

            if (result.IsValid && result.Manifest is not null)
            {
                var manifest = result.Manifest;
                var previewLines = manifest.Files
                    .OrderBy(item => item.Path, StringComparer.OrdinalIgnoreCase)
                    .Take(20)
                    .Select(item => $"• {item.Path}  ({FormatBackupSize(item.SizeBytes)})")
                    .ToList();
                if (manifest.Files.Count > previewLines.Count)
                    previewLines.Add($"… and {manifest.Files.Count - previewLines.Count:N0} more files");

                DataBackupContentsText.Text =
                    $"Created: {manifest.CreatedAtUtc.ToLocalTime():dd/MM/yyyy HH:mm:ss}{Environment.NewLine}" +
                    $"Total contents: {manifest.Files.Count:N0} files · {FormatBackupSize(result.TotalBytes)}{Environment.NewLine}" +
                    string.Join(Environment.NewLine, previewLines) +
                    $"{Environment.NewLine}{Environment.NewLine}Preview only: nothing has been extracted or restored.";
            }
            else
            {
                DataBackupContentsText.Text = "No contents preview is available because this backup did not pass verification.";
            }

            AddActivityLog(result.IsValid ? "INFO" : "WARN", "BACKUP",
                $"Backup verification: {result.Message}");
        }
        catch (Exception exception)
        {
            DataBackupStatusText.Text = $"Backup verification failed: {exception.Message}";
            AddActivityLog("ERROR", "BACKUP", $"Could not verify backup: {exception.Message}");
        }
        finally
        {
            CreateDataBackupButton.IsEnabled = true;
            VerifyDataBackupButton.IsEnabled = true;
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
                FramesPerSecond = _smoothedCaptureFps,
                PerformanceMode = GetSelectedCapturePerformanceMode().ToString()
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
                AlertMutation = AlertMutationCheckBox.IsChecked == true,
                MinimumAlertWeightKg = TryReadOptionalPositiveDouble(MinimumAlertWeightTextBox.Text, out var diagnosticWeight) ? diagnosticWeight : null,
                MinimumAlertEstimatedValue = TryReadOptionalPositiveDecimal(MinimumAlertValueTextBox.Text, out var diagnosticValue) ? diagnosticValue : null
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

    private void ExportSupportBundle_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Title = "Export GafiFish support bundle",
            Filter = "GafiFish support bundle (*.zip)|*.zip",
            DefaultExt = "zip",
            AddExtension = true,
            FileName = $"GafiFish-support-{DateTime.Now:yyyyMMdd-HHmmss}.zip",
            OverwritePrompt = true
        };
        if (dialog.ShowDialog(this) != true)
            return;

        try
        {
            var diagnostics = new
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
                    FramesPerSecond = _smoothedCaptureFps,
                    PerformanceMode = GetSelectedCapturePerformanceMode().ToString()
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
                    AutoPotion = _autoPotionEnabled
                },
                Status = new
                {
                    Vision = VisionStatusText.Text,
                    Cast = CastStatusText.Text,
                    Shake = ShakeStatusText.Text,
                    Sell = SellStatusText.Text,
                    Automation = AutomationStatusText.Text,
                    OCR = OcrStatusText.Text,
                    Session = SessionStatusText.Text
                }
            };

            using var archive = ZipFile.Open(dialog.FileName, ZipArchiveMode.Create);
            var diagnosticsEntry = archive.CreateEntry("diagnostics.json", CompressionLevel.Optimal);
            using (var writer = new StreamWriter(diagnosticsEntry.Open()))
                writer.Write(JsonSerializer.Serialize(diagnostics, new JsonSerializerOptions { WriteIndented = true }));

            var logEntry = archive.CreateEntry("activity.log", CompressionLevel.Optimal);
            using (var writer = new StreamWriter(logEntry.Open()))
                writer.Write(string.Join(Environment.NewLine, _activityLogStore.LoadRecent(500)));

            var readmeEntry = archive.CreateEntry("README.txt", CompressionLevel.Optimal);
            using (var writer = new StreamWriter(readmeEntry.Open()))
            {
                writer.WriteLine("GafiFish support bundle");
                writer.WriteLine("Contains diagnostics.json and the latest 500 local activity events.");
                writer.WriteLine("It does not include screenshots, session database, calibration files, OCR models or account credentials.");
                writer.WriteLine("Review the diagnostics and activity log before sharing this archive.");
            }

            SessionStatusText.Text = $"Support bundle exported: {System.IO.Path.GetFileName(dialog.FileName)}.";
            AddActivityLog("INFO", "DIAGNOSTICS", "Exported a privacy-limited support bundle.");
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException)
        {
            SessionStatusText.Text = $"Support bundle export failed: {exception.Message}";
            AddActivityLog("ERROR", "DIAGNOSTICS", $"Could not export support bundle: {exception.Message}");
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

    private async void ExportAllHistoryCsv_Click(object sender, RoutedEventArgs e)
    {
        await ExportAllHistoryAsync(exportJson: false);
    }

    private async void ExportAllHistoryJson_Click(object sender, RoutedEventArgs e)
    {
        await ExportAllHistoryAsync(exportJson: true);
    }

    private async Task ExportAllHistoryAsync(bool exportJson)
    {
        var extension = exportJson ? "json" : "csv";
        var dialog = new SaveFileDialog
        {
            Title = exportJson ? "Export all GafiFish catches as JSON" : "Export all GafiFish catches as CSV",
            Filter = exportJson ? "JSON files (*.json)|*.json" : "CSV files (*.csv)|*.csv",
            DefaultExt = extension,
            AddExtension = true,
            FileName = $"GafiFish-all-catches-{DateTime.Now:yyyyMMdd-HHmmss}.{extension}",
            OverwritePrompt = true
        };
        if (dialog.ShowDialog(this) != true)
            return;

        try
        {
            var history = await _sessionStore.GetAllCatchHistoryAsync();
            if (exportJson)
                await CatchHistoryExportService.ExportJsonAsync(history, dialog.FileName);
            else
                await CatchHistoryExportService.ExportCsvAsync(history, dialog.FileName);

            SessionStatusText.Text =
                $"Exported {history.Count} catches from the local archive to {System.IO.Path.GetFileName(dialog.FileName)}.";
        }
        catch (Exception exception)
        {
            SessionStatusText.Text = $"Full history export failed: {exception.Message}";
        }
    }

    private async Task ExportCompletedSessionAsync(bool exportJson)
    {
        var selectedSession = SessionHistoryList.SelectedItem as FishingSessionRecord;
        Guid? selectedSessionId = selectedSession is { EndedAt: not null }
            ? selectedSession.Id
            : selectedSession is null ? _lastCompletedSessionId : null;
        if (selectedSessionId is not Guid sessionId)
        {
            SessionStatusText.Text = "Select a completed session before exporting its catch history.";
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

            var targetFishMatched = _automationEnabled
                && _targetFishEnabled
                && !string.IsNullOrWhiteSpace(_targetFishName)
                && _targetFishStartedAt.HasValue
                && FishCatchTargetMatcher.MatchesSpecies(
                    fishCatch.Species, _targetFishName, frame.CapturedAt, _targetFishStartedAt.Value)
                && result.Confidence >= 0.90
                && _runtimeStateMachine is { CurrentState: FishingState.Reeling };

            var targetGoalReached = false;
            if (targetFishMatched && _targetCatchProgress is not null)
            {
                targetGoalReached = _targetCatchProgress.RecordMatch();
                TargetFishProgressText.Text =
                    $"Target progress · {_targetCatchProgress.Caught} / {_targetCatchProgress.Goal}";
                if (targetGoalReached && _anglerQuestAssistEnabled)
                {
                    if (_anglerQuestTracker.MarkTargetFishCaught(_targetFishName!, frame.CapturedAt))
                    {
                        AnglerQuestStatusText.Text = _anglerQuestTracker.GetStatus(frame.CapturedAt);
                        SaveAnglerQuestState("Angler target catch confirmed by OCR.");
                    }
                    AnglerQuestSubmittedButton.IsEnabled = true;
                    AnglerQuestAutomationStatusText.Text =
                        "Target fish confirmed. You can re-arm Auto Accept / Submit to hand it in, or use the manual confirmation button.";
                    AddActivityLog("INFO", "ANGLER",
                        $"Angler target caught: {_targetFishName}. Re-arm the visual Submit Fish action or confirm the hand-in manually.");
                }
            }

            var stopAfterFirstCatch = _automationEnabled
                && _keepFishingUntilCatchEnabled
                && result.Confidence >= 0.90
                && _runtimeStateMachine is { CurrentState: FishingState.Reeling };

            if (targetGoalReached || stopAfterFirstCatch)
            {
                if (!_runtimeStateMachine!.TryTransition(
                        FishingState.CatchDetected,
                        targetGoalReached
                            ? $"The target '{_targetFishName}' reached its catch goal ({_targetCatchProgress!.Caught}/{_targetCatchProgress.Goal})."
                            : "A high-confidence local OCR notification confirmed the first caught fish.",
                        frame.CapturedAt))
                {
                    StopAutomation("The catch was recognised but the fishing state could not confirm it; all inputs released.");
                    return;
                }

                // Stop on the final requested target catch before another cast or
                // timed inventory action can begin. The catch is still saved below.
                StopAutomation(targetGoalReached
                    ? $"Target goal completed: {_targetFishName} ({_targetCatchProgress!.Caught}/{_targetCatchProgress.Goal})."
                    : $"First catch confirmed ({fishCatch.Species}); keep-fishing mode completed.");
            }

            await _sessionStore.SaveCatchAsync(sessionId, fishCatch);
            await RefreshDashboardSessionAsync();
            AlertForCatchIfNeeded(fishCatch);
            await RefreshLifetimeStatisticsAsync();
            await RefreshSessionHistoryAsync(sessionId);
            if (IsCatchbookSelected())
                await RefreshCatchArchiveAsync();
            _lastCatchFingerprint = fingerprint;
            _lastCatchAt = frame.CapturedAt;
            AddActivityLog("INFO", "CATCH",
                $"{fishCatch.Species ?? "Unidentified fish"} · {fishCatch.Rarity?.ToString() ?? "rarity unknown"}" +
                (string.IsNullOrWhiteSpace(fishCatch.Mutation) ? string.Empty : $" · {fishCatch.Mutation}"));

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
        _dashboardSessionTimer.Stop();
        _aquariumTimer.Stop();
        StopAnglerQuestAutomation("Application closing; Angler inputs released.", log: false);
        StopAquariumAutomation("Application closing; Auto Aquarium inputs released.", log: false);
        StopConnectionRecovery("Application closing; reconnect watchdog inputs released.", log: false);
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

        _videoReplaySession?.Dispose();
        _videoReplaySession = null;

        _ocr?.Dispose();
        _ocr = null;

        UnregisterGlobalHotkeys();
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
