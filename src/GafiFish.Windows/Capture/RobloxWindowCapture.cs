using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace GafiFish.Windows.Capture;

/// <summary>
/// Captures only the visible client area of the foreground Roblox process.
/// No process memory, injection, hooks, or game files are accessed.
/// </summary>
public sealed class RobloxWindowCapture
{
    private const uint Srccopy = 0x00CC0020;
    private const uint CaptureBlt = 0x40000000;
    private const uint DibRgbColors = 0;
    private const uint BiRgb = 0;

    public ScreenCaptureFrame CaptureForegroundClient()
    {
        var hwnd = Native.GetForegroundWindow();
        if (hwnd == IntPtr.Zero)
        {
            throw new InvalidOperationException("There is no foreground window.");
        }

        Native.GetWindowThreadProcessId(hwnd, out var processId);
        if (processId == 0 || !IsRobloxProcess((int)processId, out var processName))
        {
            throw new InvalidOperationException(
                "Bring the Roblox game window to the foreground before capturing.");
        }

        if (!Native.GetClientRect(hwnd, out var clientRect))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not read the Roblox client bounds.");
        }

        var topLeft = new Native.Point { X = clientRect.Left, Y = clientRect.Top };
        var bottomRight = new Native.Point { X = clientRect.Right, Y = clientRect.Bottom };

        if (!Native.ClientToScreen(hwnd, ref topLeft) || !Native.ClientToScreen(hwnd, ref bottomRight))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not locate the Roblox client on screen.");
        }

        var width = bottomRight.X - topLeft.X;
        var height = bottomRight.Y - topLeft.Y;
        if (width is < 64 or > 8192 || height is < 64 or > 8192)
        {
            throw new InvalidOperationException($"Unexpected Roblox client size: {width} × {height}.");
        }

        var screenDc = Native.GetDC(IntPtr.Zero);
        if (screenDc == IntPtr.Zero)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not acquire the desktop device context.");
        }

        IntPtr memoryDc = IntPtr.Zero;
        IntPtr bitmap = IntPtr.Zero;
        IntPtr previousObject = IntPtr.Zero;

        try
        {
            memoryDc = Native.CreateCompatibleDC(screenDc);
            if (memoryDc == IntPtr.Zero)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not create a compatible device context.");
            }

            bitmap = Native.CreateCompatibleBitmap(screenDc, width, height);
            if (bitmap == IntPtr.Zero)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not create a capture bitmap.");
            }

            previousObject = Native.SelectObject(memoryDc, bitmap);
            if (previousObject == IntPtr.Zero || previousObject == new IntPtr(-1))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not select the capture bitmap.");
            }

            if (!Native.BitBlt(
                    memoryDc, 0, 0, width, height,
                    screenDc, topLeft.X, topLeft.Y,
                    Srccopy | CaptureBlt))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Screen capture failed.");
            }

            // GetDIBits requires the bitmap not to be selected into a DC.
            Native.SelectObject(memoryDc, previousObject);
            previousObject = IntPtr.Zero;

            var info = new Native.BitmapInfo
            {
                Header = new Native.BitmapInfoHeader
                {
                    Size = (uint)Marshal.SizeOf<Native.BitmapInfoHeader>(),
                    Width = width,
                    Height = -height, // top-down rows
                    Planes = 1,
                    BitCount = 32,
                    Compression = BiRgb,
                    SizeImage = (uint)(width * height * 4)
                }
            };

            var pixels = new byte[checked(width * height * 4)];
            var handle = GCHandle.Alloc(pixels, GCHandleType.Pinned);
            try
            {
                var lines = Native.GetDIBits(
                    screenDc, bitmap, 0, (uint)height,
                    handle.AddrOfPinnedObject(), ref info, DibRgbColors);

                if (lines != height)
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not read captured pixels.");
                }
            }
            finally
            {
                handle.Free();
            }

            var title = GetWindowTitle(hwnd);
            return new ScreenCaptureFrame(
                width, height, width * 4, pixels,
                DateTimeOffset.UtcNow, title, (int)processId, topLeft.X, topLeft.Y);
        }
        finally
        {
            if (previousObject != IntPtr.Zero && memoryDc != IntPtr.Zero)
            {
                Native.SelectObject(memoryDc, previousObject);
            }

            if (bitmap != IntPtr.Zero)
            {
                Native.DeleteObject(bitmap);
            }

            if (memoryDc != IntPtr.Zero)
            {
                Native.DeleteDC(memoryDc);
            }

            Native.ReleaseDC(IntPtr.Zero, screenDc);
        }
    }

    public bool IsRobloxForeground() => IsRobloxForeground(expectedProcessId: null);

    /// <summary>
    /// Verifies both the Roblox process and, when supplied, the exact process
    /// that produced the screen frame. This prevents a stale frame from driving
    /// clicks in another Roblox client window.
    /// </summary>
    public bool IsRobloxForeground(int expectedProcessId) =>
        IsRobloxForeground((int?)expectedProcessId);

    private static bool IsRobloxForeground(int? expectedProcessId)
    {
        var hwnd = Native.GetForegroundWindow();
        if (hwnd == IntPtr.Zero)
        {
            return false;
        }

        Native.GetWindowThreadProcessId(hwnd, out var processId);
        return processId != 0
            && (!expectedProcessId.HasValue || processId == expectedProcessId.Value)
            && IsRobloxProcess((int)processId, out _);
    }

    private static bool IsRobloxProcess(int processId, out string processName)
    {
        processName = string.Empty;
        try
        {
            using var process = Process.GetProcessById(processId);
            processName = process.ProcessName;
            return processName.StartsWith("RobloxPlayerBeta", StringComparison.OrdinalIgnoreCase);
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private static string GetWindowTitle(IntPtr hwnd)
    {
        var length = Native.GetWindowTextLength(hwnd);
        if (length <= 0)
        {
            return "Roblox";
        }

        var buffer = new System.Text.StringBuilder(length + 1);
        _ = Native.GetWindowText(hwnd, buffer, buffer.Capacity);
        return buffer.ToString();
    }

    private static class Native
    {
        [StructLayout(LayoutKind.Sequential)]
        internal struct Rect
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct Point
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct BitmapInfoHeader
        {
            public uint Size;
            public int Width;
            public int Height;
            public ushort Planes;
            public ushort BitCount;
            public uint Compression;
            public uint SizeImage;
            public int XPelsPerMeter;
            public int YPelsPerMeter;
            public uint ClrUsed;
            public uint ClrImportant;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct BitmapInfo
        {
            public BitmapInfoHeader Header;
            public uint Colors;
        }

        [DllImport("user32.dll")]
        internal static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetClientRect(IntPtr hWnd, out Rect rect);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool ClientToScreen(IntPtr hWnd, ref Point point);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        internal static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder text, int maxCount);

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern int ReleaseDC(IntPtr hWnd, IntPtr hDc);

        [DllImport("gdi32.dll", SetLastError = true)]
        internal static extern IntPtr CreateCompatibleDC(IntPtr hDc);

        [DllImport("gdi32.dll", SetLastError = true)]
        internal static extern IntPtr CreateCompatibleBitmap(IntPtr hDc, int width, int height);

        [DllImport("gdi32.dll", SetLastError = true)]
        internal static extern IntPtr SelectObject(IntPtr hDc, IntPtr hObject);

        [DllImport("gdi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool BitBlt(
            IntPtr destinationDc, int x, int y, int width, int height,
            IntPtr sourceDc, int sourceX, int sourceY, uint rasterOperation);

        [DllImport("gdi32.dll", SetLastError = true)]
        internal static extern int GetDIBits(
            IntPtr hDc, IntPtr bitmap, uint startScan, uint scanLines,
            IntPtr bits, ref BitmapInfo bitmapInfo, uint usage);

        [DllImport("gdi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool DeleteObject(IntPtr hObject);

        [DllImport("gdi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool DeleteDC(IntPtr hDc);
    }
}
