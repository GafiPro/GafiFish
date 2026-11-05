using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using GafiFish.Vision.Calibration;
using GafiFish.Vision.Detection;
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
    private readonly VisionProfile _profile = new();

    private HwndSource? _source;
    private CancellationTokenSource? _previewCancellation;
    private bool _captureHotkeyRegistered;
    private bool _previewHotkeyRegistered;

    public MainWindow()
    {
        InitializeComponent();
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
        var bitmap = BitmapSource.Create(
            frame.Width,
            frame.Height,
            96,
            96,
            System.Windows.Media.PixelFormats.Bgra32,
            null,
            frame.BgraPixels,
            frame.Stride);
        bitmap.Freeze();

        PreviewImage.Source = bitmap;
        PreviewPlaceholder.Visibility = Visibility.Collapsed;
        ResolutionText.Text = $"{frame.Width} × {frame.Height}";

        var visionResult = _vision.Detect(frame, _profile);
        VisionStatusText.Text = $"Vision: {visionResult.Status}";
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
