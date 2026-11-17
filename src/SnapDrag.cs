using System;
using System.Drawing;
using System.Windows.Forms;

namespace Quadra
{
    /// <summary>
    /// Drag-to-snap: watches system move/size loops, shows a preview when the cursor reaches a
    /// screen edge or corner, and lays the window out there when the drag ends.
    /// </summary>
    internal sealed class SnapDragHandler : IDisposable
    {
        private const int EdgePx = 5;
        private const int CornerPx = 60;

        private readonly WindowManager manager;
        private readonly NativeMethods.WinEventDelegate proc; // keep alive: native code holds it
        private readonly IntPtr hook;
        private readonly Timer timer;
        private readonly SnapOverlay overlay = new SnapOverlay();

        private IntPtr dragHwnd;
        private WindowAction? zone;
        private Screen zoneScreen;

        public bool Enabled { get; set; }

        public SnapDragHandler(WindowManager manager)
        {
            this.manager = manager;
            proc = OnWinEvent;
            hook = NativeMethods.SetWinEventHook(NativeMethods.EVENT_SYSTEM_MOVESIZESTART,
                NativeMethods.EVENT_SYSTEM_MOVESIZEEND, IntPtr.Zero, proc, 0, 0,
                NativeMethods.WINEVENT_OUTOFCONTEXT | NativeMethods.WINEVENT_SKIPOWNPROCESS);
            timer = new Timer { Interval = 25 };
            timer.Tick += OnTick;
        }

        private void OnWinEvent(IntPtr h, uint evt, IntPtr hwnd, int idObject, int idChild, uint thread, uint time)
        {
            if (idObject != 0) return; // OBJID_WINDOW only
            if (evt == NativeMethods.EVENT_SYSTEM_MOVESIZESTART) Begin(hwnd);
            else if (evt == NativeMethods.EVENT_SYSTEM_MOVESIZEEND) End(hwnd);
        }

        private void Begin(IntPtr hwnd)
        {
            Cancel();
            if (!Enabled || !WindowUtil.IsManageable(hwnd)) return;

            // The same event fires for resizing; only react to moves.
            int hit = WindowUtil.HitTest(hwnd, WindowUtil.CursorPosition());
            if (hit >= NativeMethods.HTLEFT && hit <= NativeMethods.HTBOTTOMRIGHT) return;

            dragHwnd = hwnd;
            timer.Start();
        }

        private void OnTick(object sender, EventArgs e)
        {
            if (dragHwnd == IntPtr.Zero) return;
            Point pt = WindowUtil.CursorPosition();
            Screen screen = Screen.FromPoint(pt);
            WindowAction? z = DetectZone(pt, screen);

            if (z == zone && zoneScreen != null && zoneScreen.DeviceName == screen.DeviceName) return;
            zone = z;
            zoneScreen = screen;

            Rectangle? preview = z.HasValue
                ? manager.Preview(z.Value, screen, WindowUtil.GetVisibleRect(dragHwnd))
                : null;
            if (preview.HasValue) overlay.ShowAt(preview.Value);
            else overlay.Hide();
        }

        private void End(IntPtr hwnd)
        {
            if (hwnd != dragHwnd) return;
            WindowAction? z = zone;
            Screen screen = zoneScreen;
            Cancel();

            if (z.HasValue) manager.Execute(hwnd, z.Value, screen);
            else manager.ForgetPosition(hwnd);
        }

        private void Cancel()
        {
            timer.Stop();
            overlay.Hide();
            dragHwnd = IntPtr.Zero;
            zone = null;
            zoneScreen = null;
        }

        private static WindowAction? DetectZone(Point p, Screen screen)
        {
            Rectangle b = screen.Bounds;
            // An edge shared with another monitor isn't a snap edge: the cursor should cross it.
            bool left = p.X <= b.Left + EdgePx && !OnAnyScreen(new Point(b.Left - 1, p.Y));
            bool right = p.X >= b.Right - 1 - EdgePx && !OnAnyScreen(new Point(b.Right, p.Y));
            bool top = p.Y <= b.Top + EdgePx && !OnAnyScreen(new Point(p.X, b.Top - 1));
            bool bottom = p.Y >= b.Bottom - 1 - EdgePx && !OnAnyScreen(new Point(p.X, b.Bottom));

            bool nearLeft = p.X <= b.Left + CornerPx, nearRight = p.X >= b.Right - 1 - CornerPx;
            bool nearTop = p.Y <= b.Top + CornerPx, nearBottom = p.Y >= b.Bottom - 1 - CornerPx;

            if ((top && nearLeft) || (left && nearTop)) return WindowAction.TopLeft;
            if ((top && nearRight) || (right && nearTop)) return WindowAction.TopRight;
            if ((bottom && nearLeft) || (left && nearBottom)) return WindowAction.BottomLeft;
            if ((bottom && nearRight) || (right && nearBottom)) return WindowAction.BottomRight;
            if (top) return WindowAction.Maximize;
            if (left) return WindowAction.LeftHalf;
            if (right) return WindowAction.RightHalf;
            if (bottom)
            {
                int third = b.Width / 3;
                if (p.X < b.Left + third) return WindowAction.FirstThird;
                if (p.X < b.Left + 2 * third) return WindowAction.CenterThird;
                return WindowAction.LastThird;
            }
            return null;
        }

        private static bool OnAnyScreen(Point p)
        {
            foreach (Screen s in Screen.AllScreens)
                if (s.Bounds.Contains(p)) return true;
            return false;
        }

        public void Dispose()
        {
            Cancel();
            if (hook != IntPtr.Zero) NativeMethods.UnhookWinEvent(hook);
            timer.Dispose();
            overlay.Dispose();
        }
    }

    /// <summary>Translucent, click-through, non-activating rectangle that previews a snap target.</summary>
    internal sealed class SnapOverlay : Form
    {
        public SnapOverlay()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            TopMost = true;
            BackColor = Color.FromArgb(0, 120, 215);
            Opacity = 0.3;
        }

        protected override bool ShowWithoutActivation
        {
            get { return true; }
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                // TOOLWINDOW | TRANSPARENT (click-through) | LAYERED | TOPMOST | NOACTIVATE
                cp.ExStyle |= 0x80 | 0x20 | 0x80000 | 0x8 | 0x08000000;
                return cp;
            }
        }

        public void ShowAt(Rectangle r)
        {
            // SetWindowPos directly so the rect stays in physical pixels on any monitor.
            NativeMethods.SetWindowPos(Handle, NativeMethods.HWND_TOPMOST, r.X, r.Y, r.Width, r.Height,
                NativeMethods.SWP_NOACTIVATE);
            if (!Visible) Show();
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            using (var pen = new Pen(Color.White, 4))
                e.Graphics.DrawRectangle(pen, 2, 2, ClientSize.Width - 4, ClientSize.Height - 4);
        }
    }
}
