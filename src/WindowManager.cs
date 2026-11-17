using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace Quadra
{
    /// <summary>Executes window actions and remembers per-window state (restore rect, size cycling).</summary>
    internal sealed class WindowManager
    {
        private const int ResizeStep = 30;
        private static readonly Size MinSize = new Size(300, 200);

        private sealed class State
        {
            public Rectangle Restore;
            public bool HasLast;
            public WindowAction LastAction;
            public Rectangle LastRect;
            public int Cycle;
        }

        private readonly Dictionary<IntPtr, State> states = new Dictionary<IntPtr, State>();

        public Config Config { get; set; }

        public WindowManager(Config config)
        {
            Config = config;
        }

        public void Execute(IntPtr hwnd, WindowAction action)
        {
            Execute(hwnd, action, null);
        }

        /// <param name="screenOverride">Screen to lay out on (drag-to-snap); null = the window's own screen.</param>
        public void Execute(IntPtr hwnd, WindowAction action, Screen screenOverride)
        {
            hwnd = WindowUtil.Root(hwnd);
            if (!WindowUtil.IsManageable(hwnd)) return;

            if (NativeMethods.IsIconic(hwnd) || NativeMethods.IsZoomed(hwnd))
                NativeMethods.ShowWindow(hwnd, NativeMethods.SW_RESTORE);

            Rectangle current = WindowUtil.GetVisibleRect(hwnd);
            State st;
            if (!states.TryGetValue(hwnd, out st))
            {
                st = new State();
                states[hwnd] = st;
            }

            if (action == WindowAction.Restore)
            {
                if (!st.Restore.IsEmpty) WindowUtil.SetVisibleRect(hwnd, st.Restore);
                states.Remove(hwnd);
                return;
            }

            // Did we put the window where it is now? If the user moved it since, start fresh.
            bool ownedByUs = st.HasLast && Near(current, st.LastRect);
            if (!ownedByUs) st.Restore = current;

            Screen screen = screenOverride ?? Screen.FromRectangle(current);
            WindowAction recorded = action;
            int cycle = 0;
            Rectangle? target;

            if (action == WindowAction.NextDisplay || action == WindowAction.PreviousDisplay)
            {
                Screen dest = AdjacentScreen(screen, action == WindowAction.NextDisplay ? 1 : -1);
                if (dest == null) return;
                bool landscape = dest.WorkingArea.Width >= dest.WorkingArea.Height;
                if (ownedByUs && Layout.Fraction(st.LastAction, landscape, st.Cycle).HasValue)
                {
                    // Keep the same layout (e.g. left half) on the new display.
                    recorded = st.LastAction;
                    cycle = st.Cycle;
                    target = ComputeTarget(recorded, dest, current, cycle);
                }
                else
                {
                    target = Layout.MapBetween(current, screen.WorkingArea, dest.WorkingArea);
                }
            }
            else
            {
                if (Config.CycleSizes && screenOverride == null && ownedByUs
                    && st.LastAction == action && Layout.IsCycling(action))
                    cycle = (st.Cycle + 1) % Layout.CycleLength;
                target = ComputeTarget(action, screen, current, cycle);
            }

            if (!target.HasValue) return;
            WindowUtil.SetVisibleRect(hwnd, target.Value);

            st.HasLast = true;
            st.LastAction = recorded;
            st.Cycle = cycle;
            st.LastRect = WindowUtil.GetVisibleRect(hwnd);
            Prune();
        }

        /// <summary>Where an action would put a window, without moving it (used for the drag preview).</summary>
        public Rectangle? Preview(WindowAction action, Screen screen, Rectangle current)
        {
            return ComputeTarget(action, screen, current, 0);
        }

        /// <summary>Called when the user drags a window by hand, so its next action starts a fresh cycle.</summary>
        public void ForgetPosition(IntPtr hwnd)
        {
            State st;
            if (states.TryGetValue(WindowUtil.Root(hwnd), out st)) st.HasLast = false;
        }

        private Rectangle? ComputeTarget(WindowAction action, Screen screen, Rectangle current, int cycle)
        {
            Rectangle wa = screen.WorkingArea;
            int gap = Math.Max(0, Config.Gap);
            RectangleF? f = Layout.Fraction(action, wa.Width >= wa.Height, cycle);
            if (f.HasValue) return Layout.ToPixels(f.Value, wa, gap);

            Rectangle inner = Rectangle.Inflate(wa, -gap, -gap);
            switch (action)
            {
                case WindowAction.MaximizeHeight:
                    return new Rectangle(current.X, inner.Y, Math.Min(current.Width, inner.Width), inner.Height);
                case WindowAction.Center:
                {
                    int w = Math.Min(current.Width, inner.Width), h = Math.Min(current.Height, inner.Height);
                    return new Rectangle(inner.X + (inner.Width - w) / 2, inner.Y + (inner.Height - h) / 2, w, h);
                }
                case WindowAction.Larger:
                    return Layout.Resize(current, inner, ResizeStep, MinSize);
                case WindowAction.Smaller:
                    return Layout.Resize(current, inner, -ResizeStep, MinSize);
                default:
                    return null;
            }
        }

        private static Screen AdjacentScreen(Screen current, int direction)
        {
            Screen[] screens = Screen.AllScreens.OrderBy(s => s.Bounds.X).ThenBy(s => s.Bounds.Y).ToArray();
            if (screens.Length < 2) return null;
            int idx = Array.FindIndex(screens, s => s.DeviceName == current.DeviceName);
            if (idx < 0) idx = 0;
            return screens[(idx + direction + screens.Length) % screens.Length];
        }

        private static bool Near(Rectangle a, Rectangle b)
        {
            const int tol = 2;
            return Math.Abs(a.Left - b.Left) <= tol && Math.Abs(a.Top - b.Top) <= tol
                && Math.Abs(a.Right - b.Right) <= tol && Math.Abs(a.Bottom - b.Bottom) <= tol;
        }

        private void Prune()
        {
            if (states.Count < 64) return;
            foreach (IntPtr dead in states.Keys.Where(h => !NativeMethods.IsWindow(h)).ToList())
                states.Remove(dead);
        }
    }
}
