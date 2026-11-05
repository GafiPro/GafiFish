using System.ComponentModel;
using System.Runtime.InteropServices;
using GafiFish.Windows.Capture;

namespace GafiFish.Windows.Input;

/// <summary>
/// Sends ordinary Windows mouse/keyboard input only while the Roblox window is
/// foreground and after an explicit Arm call. Disarming releases every held
/// input. This service is not connected to automatic gameplay in the current
/// observation build.
/// </summary>
public sealed class RobloxInputService : IDisposable
{
    private const uint InputMouse = 0;
    private const uint InputKeyboard = 1;
    private const uint MouseMove = 0x0001;
    private const uint MouseLeftDown = 0x0002;
    private const uint MouseLeftUp = 0x0004;
    private const uint MouseAbsolute = 0x8000;
    private const uint MouseVirtualDesk = 0x4000;
    private const uint KeyUp = 0x0002;

    private readonly RobloxWindowCapture _windowCapture;
    private readonly HashSet<ushort> _pressedKeys = [];
    private bool _leftButtonDown;
    private bool _isArmed;
    private bool _disposed;

    public RobloxInputService(RobloxWindowCapture? windowCapture = null)
    {
        _windowCapture = windowCapture ?? new RobloxWindowCapture();
    }

    public bool IsArmed => _isArmed;
    public bool IsLeftButtonDown => _leftButtonDown;

    public void Arm()
    {
        ThrowIfDisposed();
        if (!_windowCapture.IsRobloxForeground())
        {
            throw new InvalidOperationException("Focus the Roblox game window before arming inputs.");
        }

        _isArmed = true;
    }

    public void SetLeftButton(bool isDown)
    {
        ThrowIfDisposed();

        if (!isDown)
        {
            ReleaseLeftButton();
            return;
        }

        EnsureArmedAndForeground();
        if (_leftButtonDown)
        {
            return;
        }

        SendMouse(MouseLeftDown);
        _leftButtonDown = true;
    }

    /// <summary>
    /// Clicks a point measured in captured Roblox client pixels. The frame must
    /// be recent and the same Roblox window must still be in the foreground.
    /// </summary>
    public void ClickClient(int clientX, int clientY, ScreenCaptureFrame frame)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(frame);
        EnsureArmedAndForeground();

        var frameAge = DateTimeOffset.UtcNow - frame.CapturedAt;
        if (frameAge < TimeSpan.Zero || frameAge > TimeSpan.FromMilliseconds(250))
        {
            throw new InvalidOperationException("The screen frame is stale; capture a fresh Roblox frame before clicking.");
        }

        if (frame.ProcessId <= 0
            || clientX < 0 || clientX >= frame.Width
            || clientY < 0 || clientY >= frame.Height)
        {
            throw new ArgumentOutOfRangeException(nameof(clientX), "Click coordinates must be inside the captured client area.");
        }

        if (_leftButtonDown)
        {
            throw new InvalidOperationException("Cannot issue a point click while the left mouse button is held.");
        }

        var screenX = frame.ClientScreenX + clientX;
        var screenY = frame.ClientScreenY + clientY;
        SendMouseClickAtScreen(screenX, screenY);
    }

    public void SetKey(ushort virtualKey, bool isDown)
    {
        ThrowIfDisposed();
        if (virtualKey == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(virtualKey));
        }

        if (!isDown)
        {
            ReleaseKey(virtualKey);
            return;
        }

        EnsureArmedAndForeground();
        if (_pressedKeys.Contains(virtualKey))
        {
            return;
        }

        SendKeyboard(virtualKey, flags: 0);
        _pressedKeys.Add(virtualKey);
    }

    public void Disarm()
    {
        if (_disposed)
        {
            return;
        }

        _isArmed = false;
        ReleaseAll();
    }

    public void ReleaseAll()
    {
        ReleaseLeftButton();

        foreach (var key in _pressedKeys.ToArray())
        {
            ReleaseKey(key);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _isArmed = false;
        try
        {
            ReleaseAll();
        }
        finally
        {
            _disposed = true;
        }
    }

    private void EnsureArmedAndForeground()
    {
        if (!_isArmed)
        {
            throw new InvalidOperationException("Inputs are disarmed. Arm the input adapter explicitly first.");
        }

        if (!_windowCapture.IsRobloxForeground())
        {
            _isArmed = false;
            ReleaseAll();
            throw new InvalidOperationException("Roblox lost foreground focus. Inputs have been released and disarmed.");
        }
    }

    private void ReleaseLeftButton()
    {
        if (!_leftButtonDown)
        {
            return;
        }

        try
        {
            SendMouse(MouseLeftUp);
        }
        finally
        {
            _leftButtonDown = false;
        }
    }

    private void ReleaseKey(ushort virtualKey)
    {
        if (!_pressedKeys.Contains(virtualKey))
        {
            return;
        }

        try
        {
            SendKeyboard(virtualKey, KeyUp);
        }
        finally
        {
            _pressedKeys.Remove(virtualKey);
        }
    }

    private static void SendMouse(uint flags)
    {
        var input = new Native.Input
        {
            Type = InputMouse,
            Data = new Native.InputUnion
            {
                Mouse = new Native.MouseInput { Flags = flags }
            }
        };
        Send(input);
    }

    private static void SendMouseClickAtScreen(int screenX, int screenY)
    {
        var virtualLeft = Native.GetSystemMetrics(76);
        var virtualTop = Native.GetSystemMetrics(77);
        var virtualWidth = Native.GetSystemMetrics(78);
        var virtualHeight = Native.GetSystemMetrics(79);

        if (virtualWidth <= 1 || virtualHeight <= 1)
        {
            throw new InvalidOperationException("Could not determine the virtual desktop bounds.");
        }

        var absoluteX = Math.Clamp(
            (int)Math.Round((screenX - virtualLeft) * 65535d / (virtualWidth - 1)),
            0, 65535);
        var absoluteY = Math.Clamp(
            (int)Math.Round((screenY - virtualTop) * 65535d / (virtualHeight - 1)),
            0, 65535);

        Native.Input[] inputs =
        [
            new()
            {
                Type = InputMouse,
                Data = new Native.InputUnion
                {
                    Mouse = new Native.MouseInput
                    {
                        Dx = absoluteX,
                        Dy = absoluteY,
                        Flags = MouseMove | MouseAbsolute | MouseVirtualDesk
                    }
                }
            },
            new()
            {
                Type = InputMouse,
                Data = new Native.InputUnion
                {
                    Mouse = new Native.MouseInput { Flags = MouseLeftDown }
                }
            },
            new()
            {
                Type = InputMouse,
                Data = new Native.InputUnion
                {
                    Mouse = new Native.MouseInput { Flags = MouseLeftUp }
                }
            }
        ];

        var sent = Native.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Native.Input>());
        if (sent != inputs.Length)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Windows rejected the point-click event.");
        }
    }

    private static void SendKeyboard(ushort virtualKey, uint flags)
    {
        var input = new Native.Input
        {
            Type = InputKeyboard,
            Data = new Native.InputUnion
            {
                Keyboard = new Native.KeyboardInput
                {
                    VirtualKey = virtualKey,
                    Flags = flags
                }
            }
        };
        Send(input);
    }

    private static void Send(Native.Input input)
    {
        var sent = Native.SendInput(1, [input], Marshal.SizeOf<Native.Input>());
        if (sent != 1)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Windows rejected the input event.");
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    private static class Native
    {
        [StructLayout(LayoutKind.Sequential)]
        internal struct Input
        {
            public uint Type;
            public InputUnion Data;
        }

        [StructLayout(LayoutKind.Explicit)]
        internal struct InputUnion
        {
            [FieldOffset(0)]
            public MouseInput Mouse;

            [FieldOffset(0)]
            public KeyboardInput Keyboard;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct MouseInput
        {
            public int Dx;
            public int Dy;
            public uint MouseData;
            public uint Flags;
            public uint Time;
            public UIntPtr ExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct KeyboardInput
        {
            public ushort VirtualKey;
            public ushort ScanCode;
            public uint Flags;
            public uint Time;
            public UIntPtr ExtraInfo;
        }

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern uint SendInput(uint inputCount, [In] Input[] inputs, int size);

        [DllImport("user32.dll")]
        internal static extern int GetSystemMetrics(int index);
    }
}
