# Quadra

A keyboard-driven window manager for Windows, inspired by [Rectangle](https://github.com/rxhanson/Rectangle) for macOS.
It runs in the system tray and moves the focused window into halves, quarters, thirds, and so on, using global shortcuts or by dragging the window to a screen edge.

## Build & run

No SDK needed. It compiles with the C# compiler that ships with Windows (.NET Framework 4.x):

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1
.\bin\Quadra.exe
```

Left- or right-click the tray icon (a 2×2 grid) for the menu. From there you can run any action on the last focused window, turn on **Launch at login**, edit the config, or quit.

## Default shortcuts

These are Rectangle's defaults, with ⌃⌥ mapped to **Ctrl+Alt** and ⌘ mapped to **Win**.

| Action | Shortcut | | Action | Shortcut |
|---|---|---|---|---|
| Left / Right half | Ctrl+Alt+← / → | | Maximize | Ctrl+Alt+Enter |
| Top / Bottom half | Ctrl+Alt+↑ / ↓ | | Maximize height | Ctrl+Alt+Shift+↑ |
| Top left / Top right | Ctrl+Alt+U / I | | Larger / Smaller | Ctrl+Alt+= / - |
| Bottom left / Bottom right | Ctrl+Alt+J / K | | Center | Ctrl+Alt+C |
| First / Center / Last third | Ctrl+Alt+D / F / G | | Restore | Ctrl+Alt+Backspace |
| First / Last two thirds | Ctrl+Alt+E / T | | Next / Previous display | Ctrl+Alt+Win+→ / ← |

Center half, Center two thirds, and Almost maximize have no default shortcut. You can assign them in the config.

**Rectangle-style behaviour**
- **Size cycling:** press a half shortcut again and it cycles through ½ → ⅔ → ⅓.
- **Restore:** puts the window back where it was before Quadra first moved it.
- **Larger/Smaller:** edges that touch the screen edge stay there.
- **Thirds:** they run in columns on landscape monitors and in rows on portrait monitors.
- **Next display:** a snapped window keeps its layout on the new display. For example, a left-half window is still a left half there.
- **Drag to snap:** drag a window to a screen edge or corner to see a preview, then drop it to snap.
  - Top edge → maximize
  - Left/right edge → half
  - Corners → quarters
  - Bottom edge → thirds
  - Edges shared between monitors don't count, so you can still drag windows across.
- **Gaps:** set `Gap` in the config to add space around snapped windows.

## Configuration

`%APPDATA%\Quadra\quadra.ini` is created on first run. Edit it, then choose **Reload config** from the tray menu:

```ini
[Settings]
Gap = 0
DragToSnap = true
CycleSizes = true

[Shortcuts]
LeftHalf = Ctrl+Alt+Left
CenterHalf = Ctrl+Alt+Win+C   ; empty value = no shortcut
```

If a shortcut can't be parsed, or another app has already registered it, a tray notification tells you which one.

## Known limitations

- **Windows' own Snap feature can conflict with drag-to-snap.** If both are on, turn off *Settings → System → Multitasking → Snap windows*, or at least the "snap layouts when I drag to the top" option. Alternatively, turn off Quadra's **Drag to snap**.
- **Elevated windows (e.g. admin terminals) can't be moved by a non-elevated Quadra.** Windows blocks this. Run Quadra as admin if you need it.
- **Ctrl+Alt+letter is the same as AltGr+letter on many non-US keyboard layouts.** On those layouts the defaults can take over characters such as AltGr+E (€). Remap these in the config.
- **Some Intel graphics drivers use Ctrl+Alt+arrows to rotate the screen.** Quadra will report those shortcuts as taken.

## Layout

| File | Purpose |
|---|---|
| `src/WindowAction.cs` | Action list and pure layout math (fractions, gaps, resize, display mapping) |
| `src/WindowManager.cs` | Runs actions; per-window state for restore and size cycling |
| `src/WindowUtil.cs` | Win32 window helpers (visible-frame geometry via DWM, filtering shell windows) |
| `src/Hotkeys.cs` | Shortcut parsing and `RegisterHotKey` |
| `src/SnapDrag.cs` | Drag-to-snap hooks and the preview overlay |
| `src/TrayApp.cs` | Tray icon, menu, launch at login, wiring |
| `src/Config.cs` | INI load/save |

## License

[MIT](LICENSE). Quadra is an independent project inspired by Rectangle and is not affiliated with it.
