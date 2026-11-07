using System.Runtime.InteropServices;

namespace GafiFish.Windows.Input;

/// <summary>
/// Receives the emergency stop hotkey on a dedicated Win32 message-pump thread.
/// This keeps input release available even while the WPF dispatcher is blocked.
/// </summary>
public sealed class EmergencyStopHotkeyListener : IDisposable
{
    private const int HotkeyId = 0x47F9;
    private const uint WmHotkey = 0x0312;
    private const uint WmQuit = 0x0012;
    private const uint ModNoRepeat = 0x4000;
    private const uint PmNoRemove = 0;

    private readonly uint _virtualKey;
    private readonly Action _onPressed;
    private readonly ManualResetEventSlim _ready = new(false);
    private Thread? _thread;
    private int _threadId;
    private int _registered;
    private int _disposed;

    public EmergencyStopHotkeyListener(uint virtualKey, Action onPressed)
    {
        if (virtualKey == 0)
            throw new ArgumentOutOfRangeException(nameof(virtualKey));
        _virtualKey = virtualKey;
        _onPressed = onPressed ?? throw new ArgumentNullException(nameof(onPressed));
    }

    public bool IsRegistered => Volatile.Read(ref _registered) == 1;

    /// <summary>
    /// Starts the dedicated message pump and waits for the registration result.
    /// Returns false when Windows cannot reserve the requested hotkey.
    /// </summary>
    public bool Start(TimeSpan? timeout = null)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) == 1, this);
        if (_thread is not null)
            throw new InvalidOperationException("The emergency hotkey listener can only be started once.");

        _thread = new Thread(MessagePump)
        {
            IsBackground = true,
            Name = "GafiFish emergency stop hotkey"
        };
        try
        {
            _thread.Start();
        }
        catch
        {
            _thread = null;
            throw;
        }

        if (!_ready.Wait(timeout ?? TimeSpan.FromSeconds(2)))
        {
            Dispose();
            return false;
        }

        return IsRegistered;
    }

    private void MessagePump()
    {
        var threadId = Native.GetCurrentThreadId();

        // Force creation of this thread's message queue before publishing its id.
        // Dispose can then safely post WM_QUIT as soon as it sees a non-zero id.
        _ = Native.PeekMessage(out _, IntPtr.Zero, 0, 0, PmNoRemove);
        Volatile.Write(ref _threadId, unchecked((int)threadId));

        if (Volatile.Read(ref _disposed) == 1)
        {
            _ready.Set();
            return;
        }

        if (!Native.RegisterHotKey(IntPtr.Zero, HotkeyId, ModNoRepeat, _virtualKey))
        {
            _ready.Set();
            return;
        }

        Volatile.Write(ref _registered, 1);
        _ready.Set();

        try
        {
            if (Volatile.Read(ref _disposed) == 1)
                return;

            while (true)
            {
                var result = Native.GetMessage(out var message, IntPtr.Zero, 0, 0);
                if (result <= 0)
                    break;

                if (message.Message != WmHotkey || message.WParam.ToInt32() != HotkeyId)
                    continue;

                try
                {
                    _onPressed();
                }
                catch
                {
                    // The emergency release path must stay alive after callback failures.
                }
            }
        }
        finally
        {
            Native.UnregisterHotKey(IntPtr.Zero, HotkeyId);
            Volatile.Write(ref _registered, 0);
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
            return;

        var threadId = Volatile.Read(ref _threadId);
        if (threadId != 0)
            _ = Native.PostThreadMessage(unchecked((uint)threadId), WmQuit, UIntPtr.Zero, IntPtr.Zero);

        if (_thread is null)
        {
            _ready.Dispose();
            return;
        }

        if (_thread != Thread.CurrentThread && _thread.Join(TimeSpan.FromSeconds(2)))
            _ready.Dispose();
    }

    private static class Native
    {
        [StructLayout(LayoutKind.Sequential)]
        internal struct NativePoint
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct NativeMessage
        {
            public IntPtr WindowHandle;
            public uint Message;
            public UIntPtr WParam;
            public IntPtr LParam;
            public uint Time;
            public NativePoint Point;
        }

        [DllImport("kernel32.dll")]
        internal static extern uint GetCurrentThreadId();

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool PeekMessage(
            out NativeMessage message,
            IntPtr windowHandle,
            uint minimumMessage,
            uint maximumMessage,
            uint removeMessage);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool RegisterHotKey(
            IntPtr windowHandle,
            int id,
            uint modifiers,
            uint virtualKey);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool UnregisterHotKey(IntPtr windowHandle, int id);

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern int GetMessage(
            out NativeMessage message,
            IntPtr windowHandle,
            uint minimumMessage,
            uint maximumMessage);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool PostThreadMessage(
            uint threadId,
            uint message,
            UIntPtr wParam,
            IntPtr lParam);
    }
}
