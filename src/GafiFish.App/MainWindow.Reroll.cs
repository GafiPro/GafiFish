using System.Globalization;
using System.IO;
using System.Threading;
using System.Windows;
using GafiFish.App.Automation;
using GafiFish.App.Settings;
using GafiFish.Windows.Capture;
using GafiFish.Windows.Input;

namespace GafiFish.App;

public partial class MainWindow
{
    private readonly RerollAutomationSettingsStore _rerollAutomationSettingsStore = new();
    private RerollAutomationSettings _rerollAutomationSettings = new();
    private AutoRerollCoordinator? _autoAppraiseCoordinator;
    private RobloxInputService? _autoAppraiseInput;
    private AutoRerollCoordinator? _autoEnchantCoordinator;
    private RobloxInputService? _autoEnchantInput;
    private bool _autoAppraiseStartRequested;
    private bool _autoEnchantStartRequested;
    private int _rerollFrameProcessing;
    private DateTimeOffset _lastRerollFrameAt = DateTimeOffset.MinValue;

    private async Task LoadRerollAutomationSettingsAsync()
    {
        _rerollAutomationSettings = await _rerollAutomationSettingsStore.LoadAsync();
        AppraiseBudgetTextBox.Text = _rerollAutomationSettings.AppraiseBudget.ToString("0", CultureInfo.InvariantCulture);
        AppraiseTargetTextBox.Text = _rerollAutomationSettings.AppraiseTargetText;
        EnchantRelicLimitTextBox.Text = _rerollAutomationSettings.EnchantRelicLimit.ToString(CultureInfo.InvariantCulture);
        EnchantTargetTextBox.Text = _rerollAutomationSettings.EnchantTargetText;

        // Limits persist, but both automation switches deliberately reset to off.
        AutoAppraiseEnabledCheckBox.IsChecked = false;
        AutoEnchantEnabledCheckBox.IsChecked = false;
        AutoAppraiseStatusText.Text = _profile.IsAppraiseCalibrated
            ? "Calibration loaded. Set a target, explicitly enable Auto Appraise and press Start."
            : "Calibrate the Appraise button, exact C$ price and result text first.";
        AutoEnchantStatusText.Text = _profile.IsEnchantCalibrated
            ? "Calibration loaded. Equip the intended rod and select a relic before starting."
            : "Calibrate the altar interaction point and enchant result text first.";
    }

    private RerollAutomationSettings ReadRerollAutomationSettings()
    {
        var budgetText = AppraiseBudgetTextBox.Text
            .Replace("C$", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Trim();

        if (!decimal.TryParse(budgetText, NumberStyles.Number, CultureInfo.CurrentCulture, out var budget))
            throw new InvalidOperationException("Enter a valid Appraise budget in C$.");

        if (!int.TryParse(EnchantRelicLimitTextBox.Text, NumberStyles.Integer,
                CultureInfo.CurrentCulture, out var relicLimit))
            throw new InvalidOperationException("Enter a whole-number Enchant relic limit.");

        var settings = new RerollAutomationSettings
        {
            AppraiseBudget = budget,
            AppraiseTargetText = AppraiseTargetTextBox.Text.Trim(),
            EnchantRelicLimit = relicLimit,
            EnchantTargetText = EnchantTargetTextBox.Text.Trim()
        };
        settings.Validate();
        return settings;
    }

    private async void SaveRerollSettings_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var settings = ReadRerollAutomationSettings();
            await _rerollAutomationSettingsStore.SaveAsync(settings);
            _rerollAutomationSettings = settings;
            RerollSettingsStatusText.Text = "Appraise and Enchant limits saved locally.";
            AddActivityLog("INFO", "REROLL", "Appraise budget, relic limit and target text saved.");
        }
        catch (Exception exception) when (
            exception is ArgumentException or InvalidOperationException or IOException
            or UnauthorizedAccessException)
        {
            RerollSettingsStatusText.Text = "Could not save reroll settings: " + exception.Message;
        }
    }

    private void AutoAppraiseEnabled_Changed(object sender, RoutedEventArgs e)
    {
        if (IsLoaded && AutoAppraiseEnabledCheckBox.IsChecked != true
            && (_autoAppraiseStartRequested || _autoAppraiseCoordinator is not null))
            StopAutoAppraise("Auto Appraise disabled; inputs released.");
    }

    private void AutoEnchantEnabled_Changed(object sender, RoutedEventArgs e)
    {
        if (IsLoaded && AutoEnchantEnabledCheckBox.IsChecked != true
            && (_autoEnchantStartRequested || _autoEnchantCoordinator is not null))
            StopAutoEnchant("Auto Enchant disabled; inputs released.");
    }

    private void SelectAppraiseButtonRegion_Click(object sender, RoutedEventArgs e) =>
        SetCalibrationAction(CalibrationAction.SelectAppraiseButtonRegion,
            "Drag a tight rectangle around the Appraise button text. Do not include nearby dialogue text.");

    private void SelectAppraiseCostRegion_Click(object sender, RoutedEventArgs e) =>
        SetCalibrationAction(CalibrationAction.SelectAppraiseCostRegion,
            "Drag a tight rectangle around the current C$ price shown for one Appraise attempt.");

    private void SelectAppraiseResultRegion_Click(object sender, RoutedEventArgs e) =>
        SetCalibrationAction(CalibrationAction.SelectAppraiseResultRegion,
            "Drag a tight rectangle around the fish details that visibly change after Appraise.");

    private void SelectEnchantInteractionRegion_Click(object sender, RoutedEventArgs e) =>
        SetCalibrationAction(CalibrationAction.SelectEnchantInteractionRegion,
            "Drag a small rectangle centred on the altar interaction point. Auto Enchant clicks its centre once per confirmed result.");

    private void SelectEnchantResultRegion_Click(object sender, RoutedEventArgs e) =>
        SetCalibrationAction(CalibrationAction.SelectEnchantResultRegion,
            "Drag a tight rectangle around the enchant result text that changes after each relic use.");

    private async void StartAutoAppraise_Click(object sender, RoutedEventArgs e)
    {
        if (AutoAppraiseEnabledCheckBox.IsChecked != true)
        {
            AutoAppraiseStatusText.Text = "Enable the Auto Appraise checkbox before starting.";
            return;
        }

        if (!_profile.IsAppraiseCalibrated)
        {
            AutoAppraiseStatusText.Text = "Calibration incomplete: select the button, price and result regions.";
            return;
        }

        if (HasOtherAutomationForReroll(AutoRerollKind.Appraise))
        {
            AutoAppraiseStatusText.Text = "Stop the fishing macro and other automation modules before starting.";
            return;
        }

        try
        {
            var settings = ReadRerollAutomationSettings();
            if (string.IsNullOrWhiteSpace(settings.AppraiseTargetText))
                throw new InvalidOperationException("Enter the result text to stop on, for example a mutation name.");

            await EnsureLocalOcrEngineAsync();
            await _rerollAutomationSettingsStore.SaveAsync(settings);
            _rerollAutomationSettings = settings;
            _autoAppraiseStartRequested = true;
            StartAutoAppraiseButton.IsEnabled = false;
            StopAutoAppraiseButton.IsEnabled = true;
            AutoAppraiseStatusText.Text =
                "Start requested. Switch to Roblox; the first fresh frame will arm Auto Appraise.";
            AddActivityLog("INFO", "APPRAISE",
                "Auto Appraise requested · budget C$ " + settings.AppraiseBudget.ToString("N0", CultureInfo.InvariantCulture)
                + " · target '" + settings.AppraiseTargetText + "'.");
            if (_previewCancellation is null)
                TogglePreviewLoop();
        }
        catch (Exception exception) when (
            exception is ArgumentException or InvalidOperationException or IOException
            or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            _autoAppraiseStartRequested = false;
            StartAutoAppraiseButton.IsEnabled = true;
            StopAutoAppraiseButton.IsEnabled = false;
            AutoAppraiseStatusText.Text = "Auto Appraise could not start: " + exception.Message;
        }
    }

    private async void StartAutoEnchant_Click(object sender, RoutedEventArgs e)
    {
        if (AutoEnchantEnabledCheckBox.IsChecked != true)
        {
            AutoEnchantStatusText.Text = "Enable the Auto Enchant checkbox before starting.";
            return;
        }

        if (!_profile.IsEnchantCalibrated)
        {
            AutoEnchantStatusText.Text = "Calibration incomplete: select the altar interaction point and result text.";
            return;
        }

        if (HasOtherAutomationForReroll(AutoRerollKind.Enchant))
        {
            AutoEnchantStatusText.Text = "Stop the fishing macro and other automation modules before starting.";
            return;
        }

        try
        {
            var settings = ReadRerollAutomationSettings();
            if (string.IsNullOrWhiteSpace(settings.EnchantTargetText))
                throw new InvalidOperationException("Enter the enchantment name to stop on, for example Hasty.");

            await EnsureLocalOcrEngineAsync();
            await _rerollAutomationSettingsStore.SaveAsync(settings);
            _rerollAutomationSettings = settings;
            _autoEnchantStartRequested = true;
            StartAutoEnchantButton.IsEnabled = false;
            StopAutoEnchantButton.IsEnabled = true;
            AutoEnchantStatusText.Text =
                "Start requested. Switch to Roblox; the first fresh frame will arm Auto Enchant.";
            AddActivityLog("INFO", "ENCHANT",
                "Auto Enchant requested · relic limit " + settings.EnchantRelicLimit
                + " · target '" + settings.EnchantTargetText + "'.");
            if (_previewCancellation is null)
                TogglePreviewLoop();
        }
        catch (Exception exception) when (
            exception is ArgumentException or InvalidOperationException or IOException
            or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            _autoEnchantStartRequested = false;
            StartAutoEnchantButton.IsEnabled = true;
            StopAutoEnchantButton.IsEnabled = false;
            AutoEnchantStatusText.Text = "Auto Enchant could not start: " + exception.Message;
        }
    }

    private bool HasOtherAutomationForReroll(AutoRerollKind kind)
    {
        if (_automationEnabled || _startRequested
            || _aquariumStartRequested || _aquariumAutomationCoordinator?.IsStarted == true
            || _connectionRecoveryStartRequested || _connectionRecoveryCoordinator?.IsStarted == true
            || _anglerQuestStartRequested || _anglerQuestAutomationCoordinator?.IsStarted == true)
            return true;

        if (kind != AutoRerollKind.Appraise
            && (_autoAppraiseStartRequested || _autoAppraiseCoordinator?.IsStarted == true))
            return true;

        if (kind != AutoRerollKind.Enchant
            && (_autoEnchantStartRequested || _autoEnchantCoordinator?.IsStarted == true))
            return true;

        return false;
    }

    private void StartAutoAppraiseNow(ScreenCaptureFrame frame)
    {
        if (!_autoAppraiseStartRequested || Volatile.Read(ref _emergencyStopLatch) != 0)
            return;

        RobloxInputService? input = null;
        try
        {
            if (HasOtherAutomationForReroll(AutoRerollKind.Appraise))
                throw new InvalidOperationException("Another automation module is already active.");
            if (!_profile.IsAppraiseCalibrated || _ocr is null)
                throw new InvalidOperationException("Appraise calibration or local OCR is not ready.");
            if (_capture.GetForegroundRobloxProcessId() != frame.ProcessId)
                throw new InvalidOperationException("Roblox lost foreground focus before Auto Appraise could arm.");

            input = new RobloxInputService(_capture);
            var coordinator = new AutoRerollCoordinator(
                input,
                _capture.CaptureForegroundClient,
                _ocr.Recognize,
                AutoRerollKind.Appraise,
                _profile.AppraiseButtonRegion,
                _profile.AppraiseResultRegion,
                _rerollAutomationSettings.AppraiseTargetText,
                _profile.AppraiseCostRegion,
                _rerollAutomationSettings.AppraiseBudget);

            coordinator.Start();
            _autoAppraiseInput = input;
            _autoAppraiseCoordinator = coordinator;
            _autoAppraiseStartRequested = false;
            AutoAppraiseStatusText.Text = coordinator.LastStatus;
            AddActivityLog("INFO", "APPRAISE", "Auto Appraise armed with C$ budget protection.");
        }
        catch (Exception exception)
        {
            try { input?.EmergencyStop(); } catch { }
            try { input?.Dispose(); } catch { }
            _autoAppraiseInput = null;
            _autoAppraiseCoordinator = null;
            _autoAppraiseStartRequested = false;
            StartAutoAppraiseButton.IsEnabled = true;
            StopAutoAppraiseButton.IsEnabled = false;
            AutoAppraiseStatusText.Text = "Auto Appraise could not arm: " + exception.Message;
            AddActivityLog("WARN", "APPRAISE", AutoAppraiseStatusText.Text);
        }
    }

    private void StartAutoEnchantNow(ScreenCaptureFrame frame)
    {
        if (!_autoEnchantStartRequested || Volatile.Read(ref _emergencyStopLatch) != 0)
            return;

        RobloxInputService? input = null;
        try
        {
            if (HasOtherAutomationForReroll(AutoRerollKind.Enchant))
                throw new InvalidOperationException("Another automation module is already active.");
            if (!_profile.IsEnchantCalibrated || _ocr is null)
                throw new InvalidOperationException("Enchant calibration or local OCR is not ready.");
            if (_capture.GetForegroundRobloxProcessId() != frame.ProcessId)
                throw new InvalidOperationException("Roblox lost foreground focus before Auto Enchant could arm.");

            input = new RobloxInputService(_capture);
            var coordinator = new AutoRerollCoordinator(
                input,
                _capture.CaptureForegroundClient,
                _ocr.Recognize,
                AutoRerollKind.Enchant,
                _profile.EnchantInteractionRegion,
                _profile.EnchantResultRegion,
                _rerollAutomationSettings.EnchantTargetText,
                enchantRelicLimit: _rerollAutomationSettings.EnchantRelicLimit);

            coordinator.Start();
            _autoEnchantInput = input;
            _autoEnchantCoordinator = coordinator;
            _autoEnchantStartRequested = false;
            AutoEnchantStatusText.Text = coordinator.LastStatus;
            AddActivityLog("INFO", "ENCHANT", "Auto Enchant armed with a relic-use limit.");
        }
        catch (Exception exception)
        {
            try { input?.EmergencyStop(); } catch { }
            try { input?.Dispose(); } catch { }
            _autoEnchantInput = null;
            _autoEnchantCoordinator = null;
            _autoEnchantStartRequested = false;
            StartAutoEnchantButton.IsEnabled = true;
            StopAutoEnchantButton.IsEnabled = false;
            AutoEnchantStatusText.Text = "Auto Enchant could not arm: " + exception.Message;
            AddActivityLog("WARN", "ENCHANT", AutoEnchantStatusText.Text);
        }
    }

    private void ProcessAutoRerollFrame(ScreenCaptureFrame frame)
    {
        var appraise = _autoAppraiseCoordinator;
        var enchant = _autoEnchantCoordinator;
        var coordinator = appraise ?? enchant;
        if (coordinator is null || !coordinator.IsStarted)
            return;

        if (frame.CapturedAt - _lastRerollFrameAt < TimeSpan.FromMilliseconds(400))
            return;
        _lastRerollFrameAt = frame.CapturedAt;

        if (Interlocked.CompareExchange(ref _rerollFrameProcessing, 1, 0) != 0)
            return;

        _ = Task.Run(() =>
        {
            try
            {
                coordinator.ProcessFrame(frame);
            }
            catch (Exception exception)
            {
                coordinator.Stop("Auto reroll stopped safely after an unexpected error: " + exception.Message);
            }

            var status = coordinator.LastStatus;
            try
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (appraise is not null && ReferenceEquals(_autoAppraiseCoordinator, appraise))
                    {
                        AutoAppraiseStatusText.Text = status;
                        if (!appraise.IsStarted)
                            StopAutoAppraise(status, coordinatorAlreadyStopped: true);
                    }
                    else if (enchant is not null && ReferenceEquals(_autoEnchantCoordinator, enchant))
                    {
                        AutoEnchantStatusText.Text = status;
                        if (!enchant.IsStarted)
                            StopAutoEnchant(status, coordinatorAlreadyStopped: true);
                    }
                }));
            }
            catch (InvalidOperationException)
            {
                // The window is closing; the input service is already guarded by focus and disposal.
            }
            finally
            {
                Interlocked.Exchange(ref _rerollFrameProcessing, 0);
            }
        });
    }

    private void StopAutoAppraise_Click(object sender, RoutedEventArgs e) =>
        StopAutoAppraise("Stopped by the user; Auto Appraise inputs released.");

    private void StopAutoEnchant_Click(object sender, RoutedEventArgs e) =>
        StopAutoEnchant("Stopped by the user; Auto Enchant inputs released.");

    private void StopAutoAppraise(string reason, bool coordinatorAlreadyStopped = false)
    {
        _autoAppraiseStartRequested = false;
        try
        {
            if (!coordinatorAlreadyStopped)
                _autoAppraiseCoordinator?.Stop(reason);
            else
                _autoAppraiseInput?.Disarm();
        }
        catch { }

        try { _autoAppraiseInput?.Dispose(); } catch { }
        _autoAppraiseCoordinator = null;
        _autoAppraiseInput = null;
        StartAutoAppraiseButton.IsEnabled = true;
        StopAutoAppraiseButton.IsEnabled = false;
        AutoAppraiseStatusText.Text = reason;
        AddActivityLog("INFO", "APPRAISE", reason);
    }

    private void StopAutoEnchant(string reason, bool coordinatorAlreadyStopped = false)
    {
        _autoEnchantStartRequested = false;
        try
        {
            if (!coordinatorAlreadyStopped)
                _autoEnchantCoordinator?.Stop(reason);
            else
                _autoEnchantInput?.Disarm();
        }
        catch { }

        try { _autoEnchantInput?.Dispose(); } catch { }
        _autoEnchantCoordinator = null;
        _autoEnchantInput = null;
        StartAutoEnchantButton.IsEnabled = true;
        StopAutoEnchantButton.IsEnabled = false;
        AutoEnchantStatusText.Text = reason;
        AddActivityLog("INFO", "ENCHANT", reason);
    }
}
