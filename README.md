# Glide for Windows

A keyboard-first scrolling window manager for Windows 11, the Windows counterpart of [Glide for macOS](https://github.com/QuackByte/glide). Windows are columns on an endless horizontal strip that you glide through with the keyboard, in the style of niri and PaperWM.

Status: early. Tiling, workspaces, tabs, the overview, the Win-key shortcuts, the settings window and the focus ring work; trackpad swipes, the crash watchdog and a signed release are still to come. See [PLAN.md](PLAN.md).

## Shortcuts

Glide takes over the Windows key the way FancyZones' "Override Windows Snap" does: the combos below go to Glide instead of Snap, the taskbar and virtual desktops. Win alone still opens Start, and Win+L, Win+D, Win+E, Win+R, Win+V, Win+S, Win+X, Win+Shift+S, Win+. and Win+Alt+… are left to Windows.

| Action | Shortcut | Replaces in Windows |
| --- | --- | --- |
| Focus column left / right | Win+← / → | Snap left / right |
| Focus window up / down | Win+↑ / ↓ | Maximize / restore, minimize |
| Focus first / last column | Win+Home / End | Minimize other windows (Home) |
| Move column left / right | Win+Shift+← / → | Move to another monitor |
| Move window up / down | Win+Shift+↑ / ↓ | Stretch vertically |
| Consume or expel window left / right | Win+[ / ] | |
| Switch to workspace 1…9 | Win+1…9 | Open the Nth taskbar app |
| Send column to workspace 1…9 | Win+Shift+1…9 | New instance of the Nth taskbar app |
| Previous / next workspace | Win+Ctrl+← / → | Switch virtual desktop |
| Send column to previous / next workspace | Win+Ctrl+Shift+← / → | |
| Cycle column width (backward) | Win+Ctrl+R (+Shift) | |
| Toggle full width | Win+Ctrl+F | Find computers |
| Center column | Win+Ctrl+C | Color filters |
| Toggle tabbed column | Win+Ctrl+W | |
| Toggle floating | Win+Ctrl+V | Sound output flyout |
| Enable or disable tiling | Win+Ctrl+Shift+T | |
| Overview | Win+Tab | Task View |

Shortcuts follow key positions, like on macOS: Win+[ is the key right of P on any keyboard layout. While tiling is off, only Win+Ctrl+Shift+T stays with Glide; every other combo goes back to Windows.

### Rebinding

Open Settings from the tray menu, or launch Glide again from the Start menu while it runs. Under Shortcuts, click a shortcut and press the keys you want: any combo works, Win combos included. Esc cancels and Backspace removes it. Glide refuses combos that can't work (Win+L, which Windows handles before any app), warns about ones that shadow text editing, and tells you when a combo moves from another action.

## Settings

- **General**: turn tiling on or off, start at sign-in, the gap between windows, the slide animation, and the focus ring and its color.
- **Take over Windows shortcuts**: turn this off to give Windows back the combos Glide replaces, like FancyZones' "Override Windows Snap". Glide then only gets the shortcuts that don't clash.
- **App rules**: per app, always float, always tile or decide automatically, plus a column width and a workspace for new windows. Rules apply to open windows right away; a window you floated or tiled by hand with Win+Ctrl+V keeps your choice.
- **Restart as administrator**: Windows doesn't let a normal app move the windows of apps running as administrator, nor see keys while one is focused. Restart Glide as administrator from General to manage them too.

Everything is stored in `%APPDATA%\Glide\settings.json` and applies as you change it. The file can also be edited by hand; Glide picks up the changes when it's saved:

```json
{
  "gap": 12,
  "animate": true,
  "focusRing": true,
  "focusRingColor": "accent",
  "overrideWindowsShortcuts": true,
  "shortcuts": {
    "FocusColumnLeft": "Win+J",
    "FocusColumnRight": "Win+K",
    "CenterColumn": "none"
  },
  "rules": [
    { "appId": "SystemSettings.exe", "appName": "Settings", "mode": "Float" },
    { "appId": "WindowsTerminal.exe", "appName": "Terminal", "mode": "Tile", "widthIndex": 0, "workspace": 1 }
  ]
}
```

Action names are those in [`Action.cs`](src/Glide.Core/Action.cs). `focusRingColor` is `accent` or `#RRGGBB`. In rules, `mode` is `Automatic`, `Float` or `Tile`, `widthIndex` picks ⅓, ½, ⅔ or full (0–3), and `workspace` is zero-based.

## How it behaves

- Standard resizable app windows tile. Tool windows, owned windows and dialogs, fixed-size, cloaked and minimized windows are left alone, and so are windows of apps running as administrator (Windows doesn't let a normal app move them).
- New windows open right of the focused column and take focus. Minimized or hidden windows leave the strip and come back to the same spot.
- Off-screen columns wait as thin slivers at the screen edges; windows of other workspaces and inactive tabs wait in the bottom-right corner.
- Resizing a window with the mouse gives its column that width; dragging one snaps it back into place.
- Turning tiling off (Win+Ctrl+Shift+T or the tray menu) or quitting puts every window back on screen, including those on other workspaces. The layout is saved and comes back when tiling is turned on again, until Windows restarts.
- The focused tiled window gets a colored border (Windows 11).
- **Overview** (Win+Tab): every workspace in use as a row of live window previews, to scale. Arrows select, Enter or a click goes to the window (switching workspace if needed), typing filters by app name and title, Esc clears the filter and then closes.
- **Tabs** (Win+Ctrl+W): a column's windows become tabs, with a bar above the column. Win+↑/↓ or a click on a tab switches tabs; the bar never takes focus from the app.
- **Workspaces**: switching or sending a column to another workspace shows the workspaces in use for a moment near the bottom of the screen.
- The tray icon shows the active workspace; click it for the menu. `Glide.exe --quit` quits a running Glide.

## Build and run

Requirements: Windows 11 (x64 or ARM64) and the [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0). The Windows App SDK comes from NuGet; the app is unpackaged and self-contained, so there's nothing to install.

```powershell
dotnet build src\Glide\Glide.csproj -p:Platform=ARM64   # or x64
src\Glide\bin\ARM64\Debug\net9.0-windows10.0.19041.0\win-arm64\Glide.exe

# A self-contained folder to copy elsewhere:
dotnet publish src\Glide\Glide.csproj -c Release -p:Platform=x64 -o publish
```

### Installer

`installer\build.ps1` publishes a Release build and wraps it with [Inno Setup 6](https://jrsoftware.org/isinfo.php) (`winget install JRSoftware.InnoSetup`):

```powershell
installer\build.ps1 -Platform ARM64   # or x64; writes artifacts\Glide-0.1.0-arm64-Setup.exe
```

The installer is per-user (no administrator rights) and installs to `%LOCALAPPDATA%\Programs\Glide`. It adds a Start menu entry and, optionally, starts Glide at sign-in. Upgrading or uninstalling first asks a running Glide to put its windows back and quit. Silent install: `Setup.exe /VERYSILENT /TASKS=startup`.

Logs go to `%LOCALAPPDATA%\Glide\glide.log`; set `GLIDE_VERBOSE=1` for every action and window event.

The engine and its tests are plain .NET and build anywhere, macOS included:

```sh
dotnet test
```

## Layout

| Project | What it is |
| --- | --- |
| `src/Glide.Core` | The layout engine ported from Glide for macOS: strip, workspaces, overview selection, rules, keymap, snapshots, animation curves. No Win32. |
| `tests/Glide.Core.Tests` | xUnit tests: every test from the macOS engine, plus Windows-specific keymap tests. |
| `src/Glide.Platform` | The Win32 side, through CsWin32: window tracking, classification, layout, keyboard hook, tray icon, settings. |
| `src/Glide` | The WinUI 3 app that hosts it. |

## License

MIT
