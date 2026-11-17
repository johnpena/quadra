using System;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;

namespace Quadra
{
    /// <summary>Low-level helpers for inspecting and moving top-level windows.</summary>
    internal static class WindowUtil
    {
        private static readonly uint OwnPid = (uint)Process.GetCurrentProcess().Id;

        // Shell surfaces that should never be moved.
        private static readonly string[] IgnoredClasses =
        {
            "Progman", "WorkerW", "Shell_TrayWnd", "Shell_SecondaryTrayWnd",
            "NotifyIconOverflowWindow", "TopLevelWindowForOverflowXamlIsland",
            "Windows.UI.Core.CoreWindow", "XamlExplorerHostIslandWindow", "ForegroundStaging"
        };

        public static IntPtr Root(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero) return hwnd;
            IntPtr root = NativeMethods.GetAncestor(hwnd, NativeMethods.GA_ROOT);
            return root == IntPtr.Zero ? hwnd : root;
        }

        public static bool IsManageable(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero || !NativeMethods.IsWindow(hwnd) || !NativeMethods.IsWindowVisible(hwnd))
                return false;

            uint pid;
            NativeMethods.GetWindowThreadProcessId(hwnd, out pid);
            if (pid == OwnPid) return false;

            if ((NativeMethods.GetWindowLong(hwnd, NativeMethods.GWL_STYLE) & NativeMethods.WS_CHILD) != 0)
                return false;

            var sb = new StringBuilder(256);
            NativeMethods.GetClassName(hwnd, sb, sb.Capacity);
            string cls = sb.ToString();
            foreach (string ignored in IgnoredClasses)
                if (cls == ignored) return false;
            return true;
        }

        /// <summary>
        /// The rectangle the user actually sees. Windows 10/11 windows have invisible resize
        /// borders, so GetWindowRect is ~7px larger than the visible frame.
        /// </summary>
        public static Rectangle GetVisibleRect(IntPtr hwnd)
        {
            NativeMethods.RECT r;
            if (NativeMethods.DwmGetWindowAttribute(hwnd, NativeMethods.DWMWA_EXTENDED_FRAME_BOUNDS,
                    out r, Marshal.SizeOf(typeof(NativeMethods.RECT))) == 0)
                return Rectangle.FromLTRB(r.Left, r.Top, r.Right, r.Bottom);
            NativeMethods.GetWindowRect(hwnd, out r);
            return Rectangle.FromLTRB(r.Left, r.Top, r.Right, r.Bottom);
        }

        /// <summary>Moves the window so that its visible frame lands exactly on target.</summary>
        public static void SetVisibleRect(IntPtr hwnd, Rectangle target)
        {
            if (NativeMethods.IsZoomed(hwnd) || NativeMethods.IsIconic(hwnd))
                NativeMethods.ShowWindow(hwnd, NativeMethods.SW_RESTORE);

            // Two passes: moving to a monitor with a different DPI changes the border size
            // (and some apps resize themselves on WM_DPICHANGED), so correct after the first move.
            for (int pass = 0; pass < 2; pass++)
            {
                NativeMethods.RECT wr;
                NativeMethods.GetWindowRect(hwnd, out wr);
                Rectangle vis = GetVisibleRect(hwnd);
                int l = vis.Left - wr.Left, t = vis.Top - wr.Top;
                int r = wr.Right - vis.Right, b = wr.Bottom - vis.Bottom;

                NativeMethods.SetWindowPos(hwnd, IntPtr.Zero,
                    target.X - l, target.Y - t, target.Width + l + r, target.Height + t + b,
                    NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_NOOWNERZORDER);

                if (GetVisibleRect(hwnd) == target) break;
            }
        }

        /// <summary>WM_NCHITTEST at a screen point; 0 if the window doesn't answer quickly.</summary>
        public static int HitTest(IntPtr hwnd, Point screenPoint)
        {
            IntPtr lParam = new IntPtr((screenPoint.Y << 16) | (screenPoint.X & 0xFFFF));
            IntPtr result;
            if (NativeMethods.SendMessageTimeout(hwnd, NativeMethods.WM_NCHITTEST, IntPtr.Zero, lParam,
                    NativeMethods.SMTO_ABORTIFHUNG, 50, out result) == IntPtr.Zero)
                return 0;
            return (int)result.ToInt64();
        }

        public static Point CursorPosition()
        {
            NativeMethods.POINT p;
            NativeMethods.GetCursorPos(out p);
            return new Point(p.X, p.Y);
        }
    }
}
