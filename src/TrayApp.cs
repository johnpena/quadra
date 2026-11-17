using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Reflection;
using System.Windows.Forms;
using Microsoft.Win32;

namespace Quadra
{
    /// <summary>The app: tray icon + menu, wiring hotkeys and drag-to-snap to the window manager.</summary>
    internal sealed class TrayApp : ApplicationContext
    {
        // null = separator
        private static readonly WindowAction?[] MenuLayout =
        {
            WindowAction.LeftHalf, WindowAction.RightHalf, WindowAction.CenterHalf,
            WindowAction.TopHalf, WindowAction.BottomHalf, null,
            WindowAction.TopLeft, WindowAction.TopRight, WindowAction.BottomLeft, WindowAction.BottomRight, null,
            WindowAction.FirstThird, WindowAction.CenterThird, WindowAction.LastThird,
            WindowAction.FirstTwoThirds, WindowAction.CenterTwoThirds, WindowAction.LastTwoThirds, null,
            WindowAction.Maximize, WindowAction.AlmostMaximize, WindowAction.MaximizeHeight,
            WindowAction.Larger, WindowAction.Smaller, WindowAction.Center, WindowAction.Restore, null,
            WindowAction.NextDisplay, WindowAction.PreviousDisplay
        };

        private Config config;
        private readonly WindowManager manager;
        private readonly ForegroundTracker foreground;
        private readonly HotkeyManager hotkeys;
        private readonly SnapDragHandler drag;
        private readonly NotifyIcon tray;

        public TrayApp()
        {
            var warnings = new List<string>();
            try { config = Config.Load(warnings); }
            catch (Exception ex)
            {
                config = Config.Defaults();
                warnings.Add("Couldn't read config: " + ex.Message);
            }

            manager = new WindowManager(config);
            foreground = new ForegroundTracker();
            hotkeys = new HotkeyManager();
            hotkeys.Pressed += OnHotkey;
            drag = new SnapDragHandler(manager);

            tray = new NotifyIcon { Icon = IconFactory.Create(), Text = "Quadra", Visible = true };
            tray.MouseClick += OnTrayClick;
            SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;

            ApplyConfig(warnings);
        }

        private void ApplyConfig(List<string> warnings)
        {
            manager.Config = config;
            drag.Enabled = config.DragToSnap;

            hotkeys.UnregisterAll();
            var shown = new Dictionary<WindowAction, string>();
            foreach (KeyValuePair<WindowAction, string> kv in config.Shortcuts)
            {
                if (string.IsNullOrWhiteSpace(kv.Value)) continue;
                Hotkey hk;
                string error;
                if (!Hotkey.TryParse(kv.Value, out hk, out error))
                    warnings.Add(ActionNames.Display(kv.Key) + ": " + error);
                else if (!hotkeys.Register(kv.Key, hk))
                    warnings.Add(hk.Display + " (" + ActionNames.Display(kv.Key) + ") is taken by another app");
                else
                    shown[kv.Key] = hk.Display;
            }

            BuildMenu(shown);

            if (warnings.Count > 0)
            {
                string text = string.Join("\n", warnings);
                if (text.Length > 250) text = text.Substring(0, 247) + "...";
                tray.ShowBalloonTip(8000, "Quadra", text, ToolTipIcon.Warning);
            }
        }

        private void BuildMenu(Dictionary<WindowAction, string> shortcuts)
        {
            var menu = new ContextMenuStrip();
            menu.Items.Add(new ToolStripMenuItem("Quadra") { Enabled = false });
            menu.Items.Add(new ToolStripSeparator());

            foreach (WindowAction? entry in MenuLayout)
            {
                if (!entry.HasValue)
                {
                    menu.Items.Add(new ToolStripSeparator());
                    continue;
                }
                WindowAction action = entry.Value;
                var item = new ToolStripMenuItem(ActionNames.Display(action));
                string sc;
                if (shortcuts.TryGetValue(action, out sc)) item.ShortcutKeyDisplayString = sc;
                // The tray menu steals focus, so act on the last app window the user was in.
                item.Click += (s, e) => manager.Execute(foreground.LastWindow, action);
                menu.Items.Add(item);
            }

            menu.Items.Add(new ToolStripSeparator());
            var dragItem = new ToolStripMenuItem("Drag to snap") { Checked = config.DragToSnap };
            dragItem.Click += (s, e) =>
            {
                config.DragToSnap = !config.DragToSnap;
                drag.Enabled = config.DragToSnap;
                dragItem.Checked = config.DragToSnap;
                SaveConfig();
            };
            menu.Items.Add(dragItem);

            var cycleItem = new ToolStripMenuItem("Cycle sizes on repeat") { Checked = config.CycleSizes };
            cycleItem.Click += (s, e) =>
            {
                config.CycleSizes = !config.CycleSizes;
                cycleItem.Checked = config.CycleSizes;
                SaveConfig();
            };
            menu.Items.Add(cycleItem);

            var loginItem = new ToolStripMenuItem("Launch at login") { Checked = Startup.IsEnabled };
            loginItem.Click += (s, e) =>
            {
                Startup.Set(!Startup.IsEnabled);
                loginItem.Checked = Startup.IsEnabled;
            };
            menu.Items.Add(loginItem);

            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Edit config...", null, (s, e) => OpenConfig());
            menu.Items.Add("Reload config", null, (s, e) => ReloadConfig());
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Quit Quadra", null, (s, e) => Quit());

            ContextMenuStrip old = tray.ContextMenuStrip;
            tray.ContextMenuStrip = menu;
            if (old != null) old.Dispose();
        }

        private void OnHotkey(WindowAction action)
        {
            IntPtr hwnd = WindowUtil.Root(NativeMethods.GetForegroundWindow());
            if (WindowUtil.IsManageable(hwnd)) manager.Execute(hwnd, action);
        }

        private void OnTrayClick(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            // NotifyIcon only opens its menu on right-click; reuse the private method for left-click too.
            MethodInfo show = typeof(NotifyIcon).GetMethod("ShowContextMenu", BindingFlags.Instance | BindingFlags.NonPublic);
            if (show != null) show.Invoke(tray, null);
        }

        private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
        {
            if (e.Category != UserPreferenceCategory.General) return;
            Icon old = tray.Icon;
            tray.Icon = IconFactory.Create(); // follow light/dark taskbar
            if (old != null) old.Dispose();
        }

        private void SaveConfig()
        {
            try { config.Save(); }
            catch (Exception ex) { tray.ShowBalloonTip(5000, "Quadra", "Couldn't save config: " + ex.Message, ToolTipIcon.Error); }
        }

        private void OpenConfig()
        {
            try
            {
                if (!System.IO.File.Exists(Config.FilePath)) config.Save();
                Process.Start("notepad.exe", "\"" + Config.FilePath + "\"");
            }
            catch (Exception ex) { tray.ShowBalloonTip(5000, "Quadra", ex.Message, ToolTipIcon.Error); }
        }

        private void ReloadConfig()
        {
            var warnings = new List<string>();
            try { config = Config.Load(warnings); }
            catch (Exception ex) { warnings.Add("Couldn't read config: " + ex.Message); }
            ApplyConfig(warnings);
            if (warnings.Count == 0) tray.ShowBalloonTip(2000, "Quadra", "Config reloaded.", ToolTipIcon.Info);
        }

        private void Quit()
        {
            SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
            hotkeys.Dispose();
            drag.Dispose();
            foreground.Dispose();
            tray.Visible = false;
            tray.Dispose();
            ExitThread();
        }
    }

    /// <summary>Remembers the last real app window that had focus (the tray menu takes focus away).</summary>
    internal sealed class ForegroundTracker : IDisposable
    {
        private readonly NativeMethods.WinEventDelegate proc;
        private readonly IntPtr hook;

        public IntPtr LastWindow { get; private set; }

        public ForegroundTracker()
        {
            proc = (h, evt, hwnd, idObject, idChild, thread, time) => { if (idObject == 0) Consider(hwnd); };
            hook = NativeMethods.SetWinEventHook(NativeMethods.EVENT_SYSTEM_FOREGROUND, NativeMethods.EVENT_SYSTEM_FOREGROUND,
                IntPtr.Zero, proc, 0, 0, NativeMethods.WINEVENT_OUTOFCONTEXT | NativeMethods.WINEVENT_SKIPOWNPROCESS);
            Consider(NativeMethods.GetForegroundWindow());
        }

        private void Consider(IntPtr hwnd)
        {
            hwnd = WindowUtil.Root(hwnd);
            if (WindowUtil.IsManageable(hwnd)) LastWindow = hwnd;
        }

        public void Dispose()
        {
            if (hook != IntPtr.Zero) NativeMethods.UnhookWinEvent(hook);
        }
    }

    internal static class Startup
    {
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName = "Quadra";

        public static bool IsEnabled
        {
            get
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RunKey))
                    return k != null && k.GetValue(ValueName) != null;
            }
        }

        public static void Set(bool enabled)
        {
            using (RegistryKey k = Registry.CurrentUser.CreateSubKey(RunKey))
            {
                if (enabled) k.SetValue(ValueName, "\"" + Application.ExecutablePath + "\"");
                else k.DeleteValue(ValueName, false);
            }
        }
    }

    /// <summary>Draws the tray icon: a 2x2 grid with the top-left cell filled.</summary>
    internal static class IconFactory
    {
        public static Icon Create()
        {
            int size = Math.Max(16, SystemInformation.SmallIconSize.Width);
            Color ink = TaskbarIsLight() ? Color.FromArgb(28, 28, 28) : Color.White;

            using (var bmp = new Bitmap(size, size))
            {
                using (Graphics g = Graphics.FromImage(bmp))
                using (var brush = new SolidBrush(ink))
                using (var pen = new Pen(ink, Math.Max(1f, size / 16f)))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.Clear(Color.Transparent);
                    float pad = size * 0.08f, gap = size * 0.12f;
                    float cell = (size - 2 * pad - gap) / 2f;
                    for (int row = 0; row < 2; row++)
                    {
                        for (int col = 0; col < 2; col++)
                        {
                            var r = new RectangleF(pad + col * (cell + gap), pad + row * (cell + gap), cell, cell);
                            using (GraphicsPath path = Rounded(r, cell * 0.22f))
                            {
                                if (row == 0 && col == 0) g.FillPath(brush, path);
                                else g.DrawPath(pen, path);
                            }
                        }
                    }
                }
                IntPtr hIcon = bmp.GetHicon();
                try { return (Icon)Icon.FromHandle(hIcon).Clone(); }
                finally { NativeMethods.DestroyIcon(hIcon); }
            }
        }

        private static GraphicsPath Rounded(RectangleF r, float radius)
        {
            float d = radius * 2;
            var path = new GraphicsPath();
            path.AddArc(r.Left, r.Top, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Top, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.Left, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        private static bool TaskbarIsLight()
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(
                           @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                {
                    object v = k == null ? null : k.GetValue("SystemUsesLightTheme");
                    return v is int && (int)v == 1;
                }
            }
            catch { return false; }
        }
    }
}
