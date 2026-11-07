using System.ComponentModel;
using System.Runtime.InteropServices;

namespace GafiFish.Windows.Input;

/// <summary>
/// Production adapter for ordinary Windows SendInput events.
/// </summary>
public sealed class WindowsInputSink : IWindowsInputSink
{
    private const uint InputMouse = 0;
    private const uint InputKeyboard = 1;
    private const uint MouseMove = 0x0001;
    private const uint MouseLeftDown = 0x0002;
    private const uint MouseLeftUp = 0x0004;
    private const uint MouseAbsolute = 0x8000;
    private const uint MouseVirtualDesk = 0x4000;
    private const uint KeyUp = 0x0002;

    public void SetLeftButton(bool isDown) => SendMouse(isDown ? MouseLeftDown : MouseLeftUp);

    public void MoveCursor(int screenX, int screenY)
    {
        var virtualLeft = Native.GetSystemMetrics(76);
        var virtualTop = Native.GetSystemMetrics(77);
        var virtualWidth = Native.GetSystemMetrics(78);
        var virtualHeight = Native.GetSystemMetrics(79);
        if (virtualWidth <= 1 || virtualHeight <= 1)
            throw new InvalidOperationException("Could not determine the virtual desktop bounds.");

        var absoluteX = Math.Clamp(
            (int)Math.Round((screenX - virtualLeft) * 65535d / (virtualWidth - 1)), 0, 65535);
        var absoluteY = Math.Clamp(
            (int)Math.Round((screenY - virtualTop) * 65535d / (virtualHeight - 1)), 0, 65535);

        var input = new Native.Input
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
        };
        Send(input);
    }

    public void SetKey(ushort virtualKey, bool isDown) =>
        SendKeyboard(virtualKey, isDown ? 0 : KeyUp);

    private static void SendMouse(uint flags)
    {
        var input = new Native.Input
        {
            Type = InputMouse,
            Data = new Native.InputUnion { Mouse = new Native.MouseInput { Flags = flags } }
        };
        Send(input);
    }

    private static void SendKeyboard(ushort virtualKey, uint flags)
    {
        var input = new Native.Input
        {
            Type = InputKeyboard,
            Data = new Native.InputUnion
            {
                Keyboard = new Native.KeyboardInput { VirtualKey = virtualKey, Flags = flags }
            }
        };
        Send(input);
    }

    private static void Send(Native.Input input)
    {
        var sent = Native.SendInput(1, [input], Marshal.SizeOf<Native.Input>());
        if (sent != 1)
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Windows rejected the input event.");
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
            [FieldOffset(0)] public MouseInput Mouse;
            [FieldOffset(0)] public KeyboardInput Keyboard;
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
