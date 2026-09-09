using System;
using System.Runtime.InteropServices;

namespace GhActions.Tray;

/// <summary>
/// WPF cannot place a window in physical pixels, and Window.Left/Top are
/// DIP values whose meaning shifts under per-monitor DPI. The tray panel has
/// to land exactly above the notification area on whichever display the taskbar
/// is on, so placement goes through SetWindowPos directly.
/// </summary>
internal static class Native
{
    private static readonly IntPtr HwndTopmost = new(-1);
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpShowWindow = 0x0040;

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect { public int Left, Top, Right, Bottom; }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr hWnd, out Rect lpRect);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(
        IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    /// <summary>
    /// Park the window just inside the bottom-right of the work area on the
    /// display holding the cursor -- the work area excludes the taskbar, so
    /// this sits above it the way a tray flyout should, wherever the taskbar is.
    /// </summary>
    public static void PlaceInTrayCorner(IntPtr hwnd, int margin = 8)
    {
        if (!GetWindowRect(hwnd, out var r)) return;
        var w = r.Right - r.Left;
        var h = r.Bottom - r.Top;

        var screen = System.Windows.Forms.Screen.FromPoint(System.Windows.Forms.Control.MousePosition);
        var wa = screen.WorkingArea;

        var x = Math.Max(wa.Left, wa.Right - w - margin);
        var y = Math.Max(wa.Top, wa.Bottom - h - margin);

        SetWindowPos(hwnd, HwndTopmost, x, y, 0, 0, SwpNoSize | SwpNoActivate | SwpShowWindow);
    }
}
