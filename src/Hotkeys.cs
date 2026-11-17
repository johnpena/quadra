using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace Quadra
{
    /// <summary>A parsed shortcut such as "Ctrl+Alt+Left".</summary>
    internal sealed class Hotkey
    {
        public uint Modifiers;
        public Keys Key;
        public string Display;

        public static bool TryParse(string text, out Hotkey result, out string error)
        {
            result = null;
            error = null;
            if (string.IsNullOrWhiteSpace(text)) { error = "empty shortcut"; return false; }

            string[] parts = text.Split('+');
            uint mods = 0;
            Keys key = Keys.None;
            for (int i = 0; i < parts.Length; i++)
            {
                string p = parts[i].Trim();
                string lower = p.ToLowerInvariant();
                if (i < parts.Length - 1)
                {
                    switch (lower)
                    {
                        case "ctrl": case "control": mods |= NativeMethods.MOD_CONTROL; break;
                        case "alt": case "option": case "opt": mods |= NativeMethods.MOD_ALT; break;
                        case "shift": mods |= NativeMethods.MOD_SHIFT; break;
                        case "win": case "windows": case "super": case "cmd": mods |= NativeMethods.MOD_WIN; break;
                        default: error = "unknown modifier '" + p + "'"; return false;
                    }
                }
                else if (!TryParseKey(lower, out key))
                {
                    error = "unknown key '" + p + "'";
                    return false;
                }
            }

            var display = new List<string>();
            if ((mods & NativeMethods.MOD_CONTROL) != 0) display.Add("Ctrl");
            if ((mods & NativeMethods.MOD_ALT) != 0) display.Add("Alt");
            if ((mods & NativeMethods.MOD_SHIFT) != 0) display.Add("Shift");
            if ((mods & NativeMethods.MOD_WIN) != 0) display.Add("Win");
            display.Add(KeyName(key));

            result = new Hotkey { Modifiers = mods, Key = key, Display = string.Join("+", display) };
            return true;
        }

        private static bool TryParseKey(string k, out Keys key)
        {
            key = Keys.None;
            switch (k)
            {
                case "left": key = Keys.Left; return true;
                case "right": key = Keys.Right; return true;
                case "up": key = Keys.Up; return true;
                case "down": key = Keys.Down; return true;
                case "enter": case "return": key = Keys.Return; return true;
                case "backspace": case "back": key = Keys.Back; return true;
                case "delete": case "del": key = Keys.Delete; return true;
                case "insert": case "ins": key = Keys.Insert; return true;
                case "pageup": case "pgup": key = Keys.PageUp; return true;
                case "pagedown": case "pgdn": key = Keys.PageDown; return true;
                case "esc": case "escape": key = Keys.Escape; return true;
                case "-": case "minus": key = Keys.OemMinus; return true;
                case "=": case "equals": case "plus": key = Keys.Oemplus; return true;
                case "[": key = Keys.OemOpenBrackets; return true;
                case "]": key = Keys.OemCloseBrackets; return true;
                case ";": key = Keys.OemSemicolon; return true;
                case ",": key = Keys.Oemcomma; return true;
                case ".": key = Keys.OemPeriod; return true;
                case "/": key = Keys.OemQuestion; return true;
                case "\\": key = Keys.OemPipe; return true;
                case "`": key = Keys.Oemtilde; return true;
            }
            if (k.Length == 1 && k[0] >= 'a' && k[0] <= 'z') { key = (Keys)char.ToUpperInvariant(k[0]); return true; }
            if (k.Length == 1 && k[0] >= '0' && k[0] <= '9') { key = Keys.D0 + (k[0] - '0'); return true; }
            if (k.Length == 0 || char.IsDigit(k[0])) return false; // reject raw numeric enum values
            return Enum.TryParse(k, true, out key) && key != Keys.None;
        }

        private static string KeyName(Keys key)
        {
            switch (key)
            {
                case Keys.Left: return "←";
                case Keys.Right: return "→";
                case Keys.Up: return "↑";
                case Keys.Down: return "↓";
                case Keys.Return: return "Enter";
                case Keys.Back: return "Backspace";
                case Keys.OemMinus: return "-";
                case Keys.Oemplus: return "=";
            }
            if (key >= Keys.D0 && key <= Keys.D9) return ((char)('0' + (key - Keys.D0))).ToString();
            return key.ToString();
        }
    }

    /// <summary>Owns a message-only window that receives WM_HOTKEY for globally registered shortcuts.</summary>
    internal sealed class HotkeyManager : NativeWindow, IDisposable
    {
        private readonly Dictionary<int, WindowAction> registered = new Dictionary<int, WindowAction>();
        private int nextId = 1;

        public event Action<WindowAction> Pressed;

        public HotkeyManager()
        {
            var cp = new CreateParams { Parent = new IntPtr(-3) }; // HWND_MESSAGE
            CreateHandle(cp);
        }

        public bool Register(WindowAction action, Hotkey hotkey)
        {
            int id = nextId++;
            if (!NativeMethods.RegisterHotKey(Handle, id, hotkey.Modifiers | NativeMethods.MOD_NOREPEAT, (uint)hotkey.Key))
                return false;
            registered[id] = action;
            return true;
        }

        public void UnregisterAll()
        {
            foreach (int id in registered.Keys) NativeMethods.UnregisterHotKey(Handle, id);
            registered.Clear();
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == NativeMethods.WM_HOTKEY)
            {
                WindowAction action;
                if (registered.TryGetValue(m.WParam.ToInt32(), out action) && Pressed != null)
                    Pressed(action);
                return;
            }
            base.WndProc(ref m);
        }

        public void Dispose()
        {
            UnregisterAll();
            DestroyHandle();
        }
    }
}
