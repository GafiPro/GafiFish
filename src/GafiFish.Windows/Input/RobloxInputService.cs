using System.ComponentModel;
using System.Runtime.InteropServices;
using GafiFish.Windows.Capture;

namespace GafiFish.Windows.Input;

/// <summary>
/// Sends ordinary Windows input only to the exact Roblox process that was
/// foreground when Arm was called. Every new press/movement re-checks the
/// foreground process; losing focus disarms and releases held controls.
/// </summary>
public sealed class RobloxInputService : IRobloxInput, IDisposable
{
    private readonly object _sync = new();
    private readonly IRobloxWindowCapture _windowCapture;
    private readonly IWindowsInputSink _inputSink;
    private readonly HashSet<ushort> _pressedKeys = [];
    private bool _leftButtonDown;
    private bool _isArmed;
    private bool _emergencyStopLatched;
    private bool _disposed;
    private int? _armedProcessId;

    public RobloxInputService(
        IRobloxWindowCapture? windowCapture = null,
        IWindowsInputSink? inputSink = null)
    {
        _windowCapture = windowCapture ?? new RobloxWindowCapture();
        _inputSink = inputSink ?? new WindowsInputSink();
    }

    public bool IsArmed { get { lock (_sync) return _isArmed; } }
    public bool IsLeftButtonDown { get { lock (_sync) return _leftButtonDown; } }
    public int? ArmedProcessId { get { lock (_sync) return _armedProcessId; } }

    public void Arm()
    {
        lock (_sync)
        {
            ThrowIfDisposed();

            if (_isArmed)
            {
                EnsureArmedAndForeground();
                return;
            }

            // A previous stop/disarm may have failed to release a physical input.
            // Retry every tracked release before allowing a new automation session.
            // If Windows still rejects a release, keep the service disarmed and fail closed.
            ReleaseAll();

            var processId = _windowCapture.GetForegroundRobloxProcessId();
            if (!processId.HasValue)
                throw new InvalidOperationException(
                    "Input refused: Roblox must be the foreground application before arming.");

            if (!_windowCapture.IsRobloxForeground(processId.Value))
                throw new InvalidOperationException(
                    "Input refused: Roblox lost foreground focus while the macro was arming.");

            _armedProcessId = processId.Value;

            // Only a successful, explicit Arm call clears the emergency-stop latch.
            _emergencyStopLatched = false;
            _isArmed = true;

        }
    }

    public void SetLeftButton(bool isDown)
    {
        lock (_sync)
        {
            ThrowIfDisposed();

            if (!isDown)
            {
                ReleaseLeftButton();
                return;
            }

            EnsureArmedAndForeground();
            if (_leftButtonDown)
                return;

            EnsureArmedAndForeground();
            _inputSink.SetLeftButton(true);
            _leftButtonDown = true;

        }
    }

    public (int X, int Y)? GetCursorClientPosition(ScreenCaptureFrame frame)
    {
        lock (_sync)
        {
            ThrowIfDisposed();
            ArgumentNullException.ThrowIfNull(frame);
            EnsureArmedAndForeground();
            ValidateClientFrame(frame);

            if (!Native.GetCursorPos(out var cursor))
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Windows could not read the mouse position.");

            return (cursor.X - frame.ClientScreenX, cursor.Y - frame.ClientScreenY);

        }
    }

    public void MoveClient(int clientX, int clientY, ScreenCaptureFrame frame)
    {
        lock (_sync)
        {
            ThrowIfDisposed();
            ArgumentNullException.ThrowIfNull(frame);
            EnsureArmedAndForeground();
            ValidateClientFrame(frame);

            if (clientX < 0 || clientX >= frame.Width || clientY < 0 || clientY >= frame.Height)
                throw new ArgumentOutOfRangeException(nameof(clientX),
                    "Cursor coordinates must be inside the captured Roblox client area.");

            EnsureArmedAndForeground();
            _inputSink.MoveCursor(frame.ClientScreenX + clientX, frame.ClientScreenY + clientY);

            if (!_windowCapture.IsRobloxForeground(frame.ProcessId))
            {
                Disarm();
                throw new InvalidOperationException(
                    "The foreground Roblox process changed during cursor movement; input was released and disarmed.");
            }

        }
    }

    public void ClickClient(int clientX, int clientY, ScreenCaptureFrame frame)
    {
        lock (_sync)
        {
            ThrowIfDisposed();
            ArgumentNullException.ThrowIfNull(frame);
            EnsureArmedAndForeground();
            ValidateClientFrame(frame);

            if (clientX < 0 || clientX >= frame.Width
                || clientY < 0 || clientY >= frame.Height)
            {
                throw new ArgumentOutOfRangeException(nameof(clientX),
                    "Click coordinates must be inside the captured Roblox client area.");
            }

            if (_leftButtonDown)
                throw new InvalidOperationException(
                    "Cannot issue a point click while the left mouse button is held.");

            MoveClient(clientX, clientY, frame);
            SetLeftButton(true);
            try
            {
                SetLeftButton(false);
            }
            catch
            {
                _isArmed = false;
                try { ReleaseLeftButton(); }
                catch { /* Keep the held state tracked for a later best-effort release. */ }
                throw;
            }

        }
    }

    public void SetKey(ushort virtualKey, bool isDown)
    {
        lock (_sync)
        {
            ThrowIfDisposed();
            if (virtualKey == 0)
                throw new ArgumentOutOfRangeException(nameof(virtualKey));

            if (!isDown)
            {
                ReleaseKey(virtualKey);
                return;
            }

            EnsureArmedAndForeground();
            if (_pressedKeys.Contains(virtualKey))
                return;

            EnsureArmedAndForeground();
            _inputSink.SetKey(virtualKey, true);
            _pressedKeys.Add(virtualKey);

        }
    }

    public void EmergencyStop()
    {
        lock (_sync)
        {
            if (_disposed)
                return;

            _emergencyStopLatched = true;
            _isArmed = false;
            try
            {
                ReleaseAll();
            }
            finally
            {
                _armedProcessId = null;
            }
        }
    }

    public void Disarm()
    {
        lock (_sync)
        {
            if (_disposed)
                return;

            _isArmed = false;
            try
            {
                ReleaseAll();
            }
            finally
            {
                _armedProcessId = null;
            }

        }
    }

    public void ReleaseAll()
    {
        lock (_sync)
        {
            List<Exception>? failures = null;

            try { ReleaseLeftButton(); }
            catch (Exception exception) { (failures ??= []).Add(exception); }

            foreach (var key in _pressedKeys.ToArray())
            {
                try { ReleaseKey(key); }
                catch (Exception exception) { (failures ??= []).Add(exception); }
            }

            if (failures is { Count: > 0 })
                throw new AggregateException(
                    "Windows rejected one or more input-release events; unresolved input states remain tracked.",
                    failures);

        }
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed)
                return;

            _isArmed = false;
            try { ReleaseAll(); }
            finally
            {
                _armedProcessId = null;
                _disposed = true;
            }

        }
    }

    private void ValidateClientFrame(ScreenCaptureFrame frame)
    {
        var frameAge = DateTimeOffset.UtcNow - frame.CapturedAt;
        if (frameAge < TimeSpan.Zero || frameAge > TimeSpan.FromMilliseconds(250))
            throw new InvalidOperationException(
                "The screen frame is stale; capture a fresh Roblox frame before using the cursor.");

        if (frame.ProcessId <= 0)
            throw new ArgumentException("The captured frame has an invalid Roblox process id.", nameof(frame));

        if (_armedProcessId != frame.ProcessId
            || !_windowCapture.IsRobloxForeground(frame.ProcessId))
        {
            Disarm();
            throw new InvalidOperationException(
                "Input refused: the frame does not belong to the Roblox process armed at startup. Inputs were released.");
        }
    }

    private void EnsureArmedAndForeground()
    {
        if (_emergencyStopLatched)
            throw new InvalidOperationException(
                "Input refused: emergency stop is latched. Start the macro again explicitly to re-arm.");

        if (!_isArmed)
            throw new InvalidOperationException(
                "Input refused: macro is disarmed. Arm it while the Roblox game window is foreground.");

        if (!_armedProcessId.HasValue
            || !_windowCapture.IsRobloxForeground(_armedProcessId.Value))
        {
            _isArmed = false;
            try { ReleaseAll(); }
            finally { _armedProcessId = null; }

            throw new InvalidOperationException(
                "Input refused: Roblox is not the foreground application (or its process changed). " +
                "All held inputs were released and the macro was disarmed.");
        }
    }

    private void ReleaseLeftButton()
    {
        if (!_leftButtonDown)
            return;

        // Releases are the only events permitted after focus loss; they cannot
        // create a new click and prevent a held control remaining stuck.
        _inputSink.SetLeftButton(false);
        _leftButtonDown = false;
    }

    private void ReleaseKey(ushort virtualKey)
    {
        if (!_pressedKeys.Contains(virtualKey))
            return;

        _inputSink.SetKey(virtualKey, false);
        _pressedKeys.Remove(virtualKey);
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);

    private static class Native
    {
        [StructLayout(LayoutKind.Sequential)]
        internal struct NativePoint
        {
            public int X;
            public int Y;
        }

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetCursorPos(out NativePoint point);
    }
}
