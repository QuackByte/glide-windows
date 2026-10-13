# Glide for Windows

A keyboard-first scrolling window manager for Windows 11, the Windows counterpart of [Glide for macOS](https://github.com/QuackByte/glide). Windows are columns on an endless horizontal strip that you glide through with the keyboard, in the style of niri and PaperWM.

Status: early. Tiling, workspaces and the Win-key shortcuts work; settings UI, focus ring, tab bars, overview and trackpad swipes are still to come. See [PLAN.md](PLAN.md).

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
| Overview (not built yet; Task View still opens) | Win+Tab | Task View |

Shortcuts follow key positions, like on macOS: Win+[ is the key right of P on any keyboard layout. While tiling is off, only Win+Ctrl+Shift+T stays with Glide; every other combo goes back to Windows.

### Rebinding

Until the settings window exists, edit `%APPDATA%\Glide\settings.json` (tray menu → Open Settings File), then choose Reload Settings:

```json
{
  "gap": 12,
  "animate": true,
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

Action names are those in [`Action.cs`](src/Glide.Core/Action.cs). Binding a combo that another action uses unbinds the other one. Win+L can't be taken: Windows locks the PC before any app sees it. `mode` is `Automatic`, `Float` or `Tile`; `widthIndex` picks ⅓, ½, ⅔ or full (0–3); `workspace` is zero-based.

## How it behaves

- Standard resizable app windows tile. Tool windows, owned windows and dialogs, fixed-size, cloaked and minimized windows are left alone, and so are windows of apps running as administrator (Windows doesn't let a normal app move them).
- New windows open right of the focused column and take focus. Minimized or hidden windows leave the strip and come back to the same spot.
- Off-screen columns wait as thin slivers at the screen edges; windows of other workspaces and inactive tabs wait in the bottom-right corner.
- Resizing a window with the mouse gives its column that width; dragging one snaps it back into place.
- Turning tiling off (Win+Ctrl+Shift+T or the tray menu) or quitting puts every window back on screen, including those on other workspaces. The layout is saved and comes back when tiling is turned on again, until Windows restarts.
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
