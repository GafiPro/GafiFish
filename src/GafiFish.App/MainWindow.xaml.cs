using System.IO;
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

namespace GafiFish.App;

public partial class MainWindow : Window
{
    private const int WmHotkey = 0x0312;
    private const int CaptureHotkeyId = 0x4711;
    private const int PreviewHotkeyId = 0x4712;
    private const uint ModNoRepeat = 0x4000;
    private const uint VkF6 = 0x75;
    private const uint VkF7 = 0x76;

    private readonly RobloxWindowCapture _capture = new();
    private readonly FishingVisionDetector _vision = new();
    private readonly CastMeterVisionDetector _castVision = new();
    private readonly ShakePromptVisionDetector _shakeVision = new();
    private readonly SellAnywhereVisionDetector _sellVision = new();
    private readonly PerfectCastController _perfectCast = new();
    private readonly FishMotionTracker _motionTracker = new();
    private readonly VisionProfileStore _profileStore = new();
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

    private VisionProfile _profile = new();
    private ScreenCaptureFrame? _lastFrame;
    private HwndSource? _source;
    private CancellationTokenSource? _previewCancellation;
    private bool _captureHotkeyRegistered;
    private bool _previewHotkeyRegistered;
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
        SelectInventoryItemsRegion
    }

    public MainWindow()
    {
        InitializeComponent();
        PreviewOverlay.Children.Add(_fishMarkerOverlay);
        PreviewOverlay.Children.Add(_controlMarkerOverlay);
        Loaded += Window_Loaded;
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        _profile = await _profileStore.LoadOrDefaultAsync();
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
    }

    private void Window_SourceInitialized(object? sender, EventArgs e)
    {
        var handle = new WindowInteropHelper(this).Handle;
        _source = HwndSource.FromHwnd(handle);
        _source?.AddHook(WindowMessageHook);

        _captureHotkeyRegistered = Native.RegisterHotKey(handle, CaptureHotkeyId, ModNoRepeat, VkF6);
        _previewHotkeyRegistered = Native.RegisterHotKey(handle, PreviewHotkeyId, ModNoRepeat, VkF7);

        HotkeyStatusText.Text = (_captureHotkeyRegistered, _previewHotkeyRegistered) switch
        {
            (true, true) => "Global hotkeys ready: F6 capture · F7 observation toggle.",
            (true, false) => "F6 ready. F7 is already in use by another application.",
            (false, true) => "F7 ready. F6 is already in use by another application.",
            _ => "Global hotkeys unavailable. The buttons work while this window is active."
        };
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

        return IntPtr.Zero;
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
                if (_capture.IsRobloxForeground())
                {
                    try
                    {
                        var frame = await Task.Run(_capture.CaptureForegroundClient, token);
                        DisplayFrame(frame);
                        StatusText.Text = "Observing Roblox. Alt+Tab here to inspect the latest frame.";
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
                    }
                }

                await Task.Delay(120, token);
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
        }

        var shakeResult = _shakeVision.Detect(frame, _profile);
        ShakeStatusText.Text = shakeResult.IsCalibrated
            ? $"Shake: {shakeResult.Status}"
            : "Shake: select the prompt region and sample its color.";

        var sellResult = _sellVision.DetectButton(frame, _profile);
        SellStatusText.Text = sellResult.IsCalibrated
            ? $"Sell Anywhere: {sellResult.Status}"
            : "Sell Anywhere: select the button region and sample its color.";
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

    private void SetCalibrationAction(CalibrationAction action, string instruction)
    {
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
            or CalibrationAction.SelectInventoryItemsRegion)
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
            or CalibrationAction.SelectInventoryItemsRegion)
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
            or CalibrationAction.SelectInventoryItemsRegion)
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
                ReferenceWidth = _lastFrame.Width,
                ReferenceHeight = _lastFrame.Height
            },
            CalibrationAction.SelectCastRegion => _profile with
            {
                CastRegion = selectedRegion,
                ReferenceWidth = _lastFrame.Width,
                ReferenceHeight = _lastFrame.Height
            },
            CalibrationAction.SelectShakeRegion => _profile with
            {
                ShakeRegion = selectedRegion,
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

    private void Window_Closed(object? sender, EventArgs e)
    {
        StopPreviewLoop();

        var handle = new WindowInteropHelper(this).Handle;
        if (_captureHotkeyRegistered)
        {
            Native.UnregisterHotKey(handle, CaptureHotkeyId);
        }

        if (_previewHotkeyRegistered)
        {
            Native.UnregisterHotKey(handle, PreviewHotkeyId);
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
