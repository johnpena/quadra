using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Quadra
{
    /// <summary>Settings stored in %APPDATA%\Quadra\quadra.ini.</summary>
    internal sealed class Config
    {
        public int Gap;
        public bool DragToSnap = true;
        public bool CycleSizes = true;
        public readonly Dictionary<WindowAction, string> Shortcuts = new Dictionary<WindowAction, string>();

        public static readonly string Dir =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Quadra");
        public static readonly string FilePath = Path.Combine(Dir, "quadra.ini");

        /// <summary>Rectangle's defaults, with Control+Option mapped to Ctrl+Alt and Command to Win.</summary>
        public static Config Defaults()
        {
            var c = new Config();
            var s = c.Shortcuts;
            s[WindowAction.LeftHalf] = "Ctrl+Alt+Left";
            s[WindowAction.RightHalf] = "Ctrl+Alt+Right";
            s[WindowAction.CenterHalf] = "";
            s[WindowAction.TopHalf] = "Ctrl+Alt+Up";
            s[WindowAction.BottomHalf] = "Ctrl+Alt+Down";
            s[WindowAction.TopLeft] = "Ctrl+Alt+U";
            s[WindowAction.TopRight] = "Ctrl+Alt+I";
            s[WindowAction.BottomLeft] = "Ctrl+Alt+J";
            s[WindowAction.BottomRight] = "Ctrl+Alt+K";
            s[WindowAction.FirstThird] = "Ctrl+Alt+D";
            s[WindowAction.CenterThird] = "Ctrl+Alt+F";
            s[WindowAction.LastThird] = "Ctrl+Alt+G";
            s[WindowAction.FirstTwoThirds] = "Ctrl+Alt+E";
            s[WindowAction.CenterTwoThirds] = "";
            s[WindowAction.LastTwoThirds] = "Ctrl+Alt+T";
            s[WindowAction.Maximize] = "Ctrl+Alt+Enter";
            s[WindowAction.AlmostMaximize] = "";
            s[WindowAction.MaximizeHeight] = "Ctrl+Alt+Shift+Up";
            s[WindowAction.Larger] = "Ctrl+Alt+=";
            s[WindowAction.Smaller] = "Ctrl+Alt+-";
            s[WindowAction.Center] = "Ctrl+Alt+C";
            s[WindowAction.Restore] = "Ctrl+Alt+Backspace";
            s[WindowAction.NextDisplay] = "Ctrl+Alt+Win+Right";
            s[WindowAction.PreviousDisplay] = "Ctrl+Alt+Win+Left";
            return c;
        }

        public static Config Load(List<string> warnings)
        {
            Config c = Defaults();
            if (!File.Exists(FilePath))
            {
                c.Save();
                return c;
            }

            string section = "";
            int lineNo = 0;
            foreach (string raw in File.ReadAllLines(FilePath))
            {
                lineNo++;
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith(";") || line.StartsWith("#")) continue;
                if (line.StartsWith("[") && line.EndsWith("]"))
                {
                    section = line.Substring(1, line.Length - 2).Trim().ToLowerInvariant();
                    continue;
                }
                int eq = line.IndexOf('=');
                if (eq < 0)
                {
                    warnings.Add("Line " + lineNo + ": expected key = value");
                    continue;
                }
                string key = line.Substring(0, eq).Trim();
                string value = line.Substring(eq + 1).Trim();

                if (section == "settings")
                {
                    switch (key.ToLowerInvariant())
                    {
                        case "gap":
                            int gap;
                            if (int.TryParse(value, out gap) && gap >= 0) c.Gap = Math.Min(gap, 200);
                            else warnings.Add("Line " + lineNo + ": Gap must be a whole number");
                            break;
                        case "dragtosnap": c.DragToSnap = ParseBool(value, c.DragToSnap); break;
                        case "cyclesizes": c.CycleSizes = ParseBool(value, c.CycleSizes); break;
                        default: warnings.Add("Line " + lineNo + ": unknown setting '" + key + "'"); break;
                    }
                }
                else if (section == "shortcuts")
                {
                    WindowAction action;
                    if (!Enum.TryParse(key, true, out action) || !Enum.IsDefined(typeof(WindowAction), action))
                        warnings.Add("Line " + lineNo + ": unknown action '" + key + "'");
                    else
                        c.Shortcuts[action] = value;
                }
            }
            return c;
        }

        private static bool ParseBool(string value, bool fallback)
        {
            switch (value.ToLowerInvariant())
            {
                case "true": case "yes": case "on": case "1": return true;
                case "false": case "no": case "off": case "0": return false;
                default: return fallback;
            }
        }

        public void Save()
        {
            var sb = new StringBuilder();
            sb.AppendLine("; Quadra configuration. Edit, save, then choose \"Reload config\" from the tray menu.");
            sb.AppendLine(";");
            sb.AppendLine("; Shortcut syntax: modifiers (Ctrl, Alt, Shift, Win) joined with '+', then one key.");
            sb.AppendLine("; Keys: A-Z, 0-9, F1-F24, Left, Right, Up, Down, Enter, Backspace, Delete, Home, End,");
            sb.AppendLine(";       PageUp, PageDown, Space, - and =. Leave a value empty to disable that shortcut.");
            sb.AppendLine();
            sb.AppendLine("[Settings]");
            sb.AppendLine("; Pixels of space around snapped windows.");
            sb.AppendLine("Gap = " + Gap);
            sb.AppendLine("; Drag a window to a screen edge or corner to snap it (preview shows where it will go).");
            sb.AppendLine("DragToSnap = " + (DragToSnap ? "true" : "false"));
            sb.AppendLine("; Repeating a half shortcut cycles its size through 1/2, 2/3 and 1/3.");
            sb.AppendLine("CycleSizes = " + (CycleSizes ? "true" : "false"));
            sb.AppendLine();
            sb.AppendLine("[Shortcuts]");
            foreach (WindowAction a in Enum.GetValues(typeof(WindowAction)))
            {
                string v;
                Shortcuts.TryGetValue(a, out v);
                sb.AppendLine(a + " = " + (v ?? ""));
            }

            Directory.CreateDirectory(Dir);
            File.WriteAllText(FilePath, sb.ToString(), new UTF8Encoding(false));
        }
    }
}
