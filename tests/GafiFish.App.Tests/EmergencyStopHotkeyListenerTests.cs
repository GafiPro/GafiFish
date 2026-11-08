using System.Collections.Concurrent;
using GafiFish.Windows.Input;
using GafiFish.Windows.Capture;
using Xunit;

namespace GafiFish.App.Tests;

public sealed class EmergencyStopHotkeyListenerTests
{
    [Fact]
    public void Start_RunsCallbackOnDedicatedMessagePumpThreadAndUnregistersOnDispose()
    {
        using var platform = new FakeHotkeyPlatform();
        using var callbackCompleted = new ManualResetEventSlim();
        var callbackThreadId = 0;
        using var listener = new EmergencyStopHotkeyListener(
            0x78,
            () =>
            {
                callbackThreadId = Environment.CurrentManagedThreadId;
                callbackCompleted.Set();
            },
            platform);

        var callerThreadId = Environment.CurrentManagedThreadId;

        Assert.True(listener.Start());
        Assert.True(listener.IsRegistered);
        Assert.True(platform.PostHotkey(0x47F9));
        Assert.True(callbackCompleted.Wait(TimeSpan.FromSeconds(2)));
        Assert.NotEqual(callerThreadId, callbackThreadId);

        listener.Dispose();

        Assert.True(platform.Unregistered.Wait(TimeSpan.FromSeconds(2)));
        Assert.False(listener.IsRegistered);
        Assert.Equal(1, platform.UnregisterCount);
    }

    [Fact]
    public void CallbackFailureDoesNotTerminateTheEmergencyMessagePump()
    {
        using var platform = new FakeHotkeyPlatform();
        using var secondCallbackCompleted = new ManualResetEventSlim();
        var calls = 0;
        using var listener = new EmergencyStopHotkeyListener(
            0x78,
            () =>
            {
                var count = Interlocked.Increment(ref calls);
                if (count == 1)
                    throw new InvalidOperationException("Simulated emergency callback failure.");
                secondCallbackCompleted.Set();
            },
            platform);

        Assert.True(listener.Start());

        // An unrelated hotkey ID is ignored.
        Assert.True(platform.PostMessage(new EmergencyHotkeyMessage(
            1, 0x0312, 0x1111)));
        Assert.True(platform.PostHotkey(0x47F9));
        Assert.True(platform.PostHotkey(0x47F9));

        Assert.True(secondCallbackCompleted.Wait(TimeSpan.FromSeconds(2)));
        Assert.Equal(2, Volatile.Read(ref calls));
        Assert.True(listener.IsRegistered);

        listener.Dispose();
        Assert.True(platform.Unregistered.Wait(TimeSpan.FromSeconds(2)));
    }

    [Fact]
    public void EmergencyHotkey_ReleasesHeldInputsWhileCallingThreadIsBlocked()
    {
        using var platform = new FakeHotkeyPlatform();
        var sink = new RecordingInputSink();
        using var input = new RobloxInputService(new FakeRobloxWindowCapture(), sink);
        using var stopCompleted = new ManualResetEventSlim();

        input.Arm();
        input.SetLeftButton(true);
        input.SetKey(0x41, true);

        using var listener = new EmergencyStopHotkeyListener(
            0x78,
            () =>
            {
                input.EmergencyStop();
                stopCompleted.Set();
            },
            platform);

        Assert.True(listener.Start());
        Assert.True(platform.PostHotkey(0x47F9));

        // The calling thread blocks here while the independent message-pump
        // thread performs the input release.
        Assert.True(stopCompleted.Wait(TimeSpan.FromSeconds(2)));
        Assert.False(input.IsArmed);
        Assert.False(input.IsLeftButtonDown);
        Assert.Contains(("left", (ushort)0, false), sink.Events);
        Assert.Contains(("key", (ushort)0x41, false), sink.Events);

        listener.Dispose();
        Assert.True(platform.Unregistered.Wait(TimeSpan.FromSeconds(2)));
    }

    [Fact]
    public void Start_ReturnsFalseWhenPlatformCannotRegisterTheHotkey()
    {
        using var platform = new FakeHotkeyPlatform { RegistrationSucceeds = false };
        using var listener = new EmergencyStopHotkeyListener(0x78, () => { }, platform);

        Assert.False(listener.Start());
        Assert.False(listener.IsRegistered);
        Assert.Equal(0, platform.UnregisterCount);

        listener.Dispose();
    }

    private sealed class FakeHotkeyPlatform : IEmergencyStopHotkeyPlatform, IDisposable
    {
        private readonly BlockingCollection<EmergencyHotkeyMessage> _messages = new();
        private int _listenerThreadId;
        private int _unregisterCount;

        public bool RegistrationSucceeds { get; init; } = true;
        public int UnregisterCount => Volatile.Read(ref _unregisterCount);
        public ManualResetEventSlim Unregistered { get; } = new(false);

        public uint GetCurrentThreadId() =>
            unchecked((uint)Environment.CurrentManagedThreadId);

        public void EnsureMessageQueue()
        {
        }

        public bool RegisterHotKey(int hotkeyId, uint modifiers, uint virtualKey)
        {
            _listenerThreadId = Environment.CurrentManagedThreadId;
            return RegistrationSucceeds;
        }

        public EmergencyHotkeyMessage GetMessage() => _messages.Take();

        public bool PostQuitMessage(uint threadId)
        {
            if (threadId != unchecked((uint)_listenerThreadId))
                return false;

            return PostMessage(new EmergencyHotkeyMessage(0, 0, 0));
        }

        public void UnregisterHotKey(int hotkeyId)
        {
            Interlocked.Increment(ref _unregisterCount);
            Unregistered.Set();
        }

        public bool PostHotkey(int hotkeyId) =>
            PostMessage(new EmergencyHotkeyMessage(1, 0x0312, unchecked((nuint)hotkeyId)));

        public bool PostMessage(EmergencyHotkeyMessage message) =>
            !_messages.IsAddingCompleted && _messages.TryAdd(message);

        public void Dispose()
        {
            _messages.Dispose();
            Unregistered.Dispose();
        }
    }

    private sealed class FakeRobloxWindowCapture : IRobloxWindowCapture
    {
        private const int ProcessId = 12345;

        public ScreenCaptureFrame CaptureForegroundClient() =>
            throw new NotSupportedException("The emergency-stop test does not capture the screen.");

        public bool IsRobloxForeground() => true;
        public bool IsRobloxForeground(int expectedProcessId) => expectedProcessId == ProcessId;
        public int? GetForegroundRobloxProcessId() => ProcessId;
    }

    private sealed class RecordingInputSink : IWindowsInputSink
    {
        public List<(string Kind, ushort Key, bool IsDown)> Events { get; } = [];

        public void SetLeftButton(bool isDown) => Events.Add(("left", 0, isDown));
        public void MoveCursor(int screenX, int screenY) { }
        public void SetKey(ushort virtualKey, bool isDown) =>
            Events.Add(("key", virtualKey, isDown));
    }
}
