using System.Runtime.InteropServices;

namespace GafiFish.Windows.Input;

/// <summary>
/// One result from a platform message pump. A non-positive Result terminates
/// the pump, matching the Win32 GetMessage contract.
/// </summary>
public readonly record struct EmergencyHotkeyMessage(
    int Result,
    uint Message,
    nuint WParam);

/// <summary>
/// Small platform seam so the dedicated emergency message-pump thread can be
/// verified without invoking global Win32 hotkeys in unit tests.
/// </summary>
public interface IEmergencyStopHotkeyPlatform
{
    uint GetCurrentThreadId();
    void EnsureMessageQueue();
    bool RegisterHotKey(int hotkeyId, uint modifiers, uint virtualKey);
    EmergencyHotkeyMessage GetMessage();
    bool PostQuitMessage(uint threadId);
    void UnregisterHotKey(int hotkeyId);
}

/// <summary>
/// Receives the emergency stop hotkey on a dedicated message-pump thread.
/// This keeps input release available even while the WPF dispatcher is blocked.
/// </summary>
public sealed class EmergencyStopHotkeyListener : IDisposable
{
    private const int HotkeyId = 0x47F9;
    private const uint WmHotkey = 0x0312;
    private const uint WmQuit = 0x0012;
    private const uint ModNoRepeat = 0x4000;

    private readonly uint _virtualKey;
    private readonly Action _onPressed;
    private readonly IEmergencyStopHotkeyPlatform _platform;
    private readonly ManualResetEventSlim _ready = new(false);
    private Thread? _thread;
    private int _threadId;
    private int _registered;
    private int _disposed;
    private Exception? _failure;

    public EmergencyStopHotkeyListener(uint virtualKey, Action onPressed)
        : this(virtualKey, onPressed, new Win32EmergencyStopHotkeyPlatform())
    {
    }

    public EmergencyStopHotkeyListener(
        uint virtualKey,
        Action onPressed,
        IEmergencyStopHotkeyPlatform platform)
    {
        if (virtualKey == 0)
            throw new ArgumentOutOfRangeException(nameof(virtualKey));
        _virtualKey = virtualKey;
        _onPressed = onPressed ?? throw new ArgumentNullException(nameof(onPressed));
        _platform = platform ?? throw new ArgumentNullException(nameof(platform));
    }

    public bool IsRegistered => Volatile.Read(ref _registered) == 1;
    public string? LastError => Volatile.Read(ref _failure)?.Message;

    /// <summary>
    /// Starts the dedicated message pump and waits for the registration result.
    /// Returns false when the platform cannot reserve the requested hotkey.
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

        var failure = Volatile.Read(ref _failure);
        if (failure is not null)
            throw new InvalidOperationException("The emergency hotkey listener could not start.", failure);

        return IsRegistered;
    }

    private void MessagePump()
    {
        try
        {
            var threadId = _platform.GetCurrentThreadId();

            // Force creation of this thread's message queue before publishing its id.
            // Dispose can then safely post WM_QUIT as soon as it sees a non-zero id.
            _platform.EnsureMessageQueue();
            Volatile.Write(ref _threadId, unchecked((int)threadId));

            if (Volatile.Read(ref _disposed) == 1)
            {
                _ready.Set();
                return;
            }

            if (!_platform.RegisterHotKey(HotkeyId, ModNoRepeat, _virtualKey))
            {
                _ready.Set();
                return;
            }

            Volatile.Write(ref _registered, 1);
            _ready.Set();

            if (Volatile.Read(ref _disposed) == 1)
                return;

            while (true)
            {
                var message = _platform.GetMessage();
                if (message.Result <= 0)
                    break;

                if (message.Message != WmHotkey || message.WParam != (nuint)HotkeyId)
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
        catch (Exception exception)
        {
            Volatile.Write(ref _failure, exception);
            _ready.Set();
        }
        finally
        {
            if (IsRegistered)
            {
                try
                {
                    _platform.UnregisterHotKey(HotkeyId);
                }
                catch (Exception exception)
                {
                    Volatile.Write(ref _failure, exception);
                }
                finally
                {
                    Volatile.Write(ref _registered, 0);
                }
            }
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
            return;

        var threadId = Volatile.Read(ref _threadId);
        if (threadId != 0)
        {
            try { _ = _platform.PostQuitMessage(unchecked((uint)threadId)); }
            catch (Exception exception) { Volatile.Write(ref _failure, exception); }
        }

        if (_thread is null)
        {
            _ready.Dispose();
            return;
        }

        if (_thread != Thread.CurrentThread && _thread.Join(TimeSpan.FromSeconds(2)))
            _ready.Dispose();
    }

    private sealed class Win32EmergencyStopHotkeyPlatform : IEmergencyStopHotkeyPlatform
    {
        private const uint PmNoRemove = 0;

        public uint GetCurrentThreadId() => Native.GetCurrentThreadId();

        public void EnsureMessageQueue() =>
            _ = Native.PeekMessage(out _, IntPtr.Zero, 0, 0, PmNoRemove);

        public bool RegisterHotKey(int hotkeyId, uint modifiers, uint virtualKey) =>
            Native.RegisterHotKey(IntPtr.Zero, hotkeyId, modifiers, virtualKey);

        public EmergencyHotkeyMessage GetMessage()
        {
            var result = Native.GetMessage(out var message, IntPtr.Zero, 0, 0);
            return new EmergencyHotkeyMessage(
                result, message.Message, unchecked((nuint)message.WParam.ToUInt64()));
        }

        public bool PostQuitMessage(uint threadId) =>
            Native.PostThreadMessage(threadId, WmQuit, UIntPtr.Zero, IntPtr.Zero);

        public void UnregisterHotKey(int hotkeyId) =>
            _ = Native.UnregisterHotKey(IntPtr.Zero, hotkeyId);
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
