# Glide for Windows

A keyboard-first scrolling window manager for Windows 11, the Windows counterpart of [Glide for macOS](https://github.com/QuackByte/glide). Windows are columns on an endless horizontal strip that you glide through with the keyboard, in the style of niri and PaperWM. Prefer everything on screen? Any workspace can switch to a tiled layout instead.

Status: early. Tiling on every monitor, workspaces, stacks and tabs, the overview, the Win-key shortcuts, mouse-wheel and touchpad scrolling, the settings window, the focus ring and the crash watchdog work. See [PLAN.md](PLAN.md), and [quackbyte.dev/glide](https://quackbyte.dev/glide/#platforms) for what each platform has.

## Install

Download the installer for your PC (x64, or ARM64 for Arm PCs such as Snapdragon laptops) from the [latest release](https://github.com/QuackByte/glide-windows/releases/latest) or [quackbyte.dev/glide](https://quackbyte.dev/glide/). It installs for your user only, without administrator rights, and keeps itself up to date.

Releases aren't code-signed yet, so Windows SmartScreen may warn the first time you run Glide: choose **More info › Run anyway**.

## Shortcuts

Glide takes over the Windows key the way FancyZones' "Override Windows Snap" does: the combos below go to Glide instead of Snap, the taskbar and virtual desktops. Win alone still opens Start, and Win+L, Win+D, Win+E, Win+R, Win+V, Win+S, Win+X, Win+Shift+S, Win+. and Win+Alt+… are left to Windows.

| Action | Shortcut | Replaces in Windows |
| --- | --- | --- |
| Focus column left / right | Win+← / → | Snap left / right |
| Focus window up / down | Win+↑ / ↓ | Maximize / restore, minimize |
| Focus first / last column | Win+Home / End | Minimize other windows (Home) |
| Move column left / right | Win+Shift+← / → | Move to another monitor |
| Move window up / down | Win+Shift+↑ / ↓ | Stretch vertically |
| Stack into the column on the left / right, or unstack | Win+[ / ] (the two keys right of P) | |
| Switch to workspace 1…9 | Win+1…9 | Open the Nth taskbar app |
| Send window to workspace 1…9 | Win+Shift+1…9 | New instance of the Nth taskbar app |
| Previous / next workspace | Win+Ctrl+← / → | Switch virtual desktop |
| Send window to previous / next workspace | Win+Ctrl+Shift+← / → | |
| Cycle column width (backward); tiled: the split | Win+Ctrl+R (+Shift) | |
| Toggle full width; tiled: zoom the tile | Win+Ctrl+F | Find computers |
| Center column | Win+Ctrl+C | Color filters |
| Toggle tabbed column | Win+Ctrl+W | |
| Switch the workspace between scrolling and tiled | Win+Ctrl+T | |
| Switch the tiled workspace between 2 and 3 columns | Win+Ctrl+G | |
| Toggle floating | Win+Ctrl+V | Sound output flyout |
| Focus monitor left / right | Win+Alt+← / → | |
| Send column to monitor left / right | Win+Alt+Shift+← / → | |
| Enable or disable tiling | Win+Ctrl+Shift+T | |
| Overview | Win+Tab | Task View |
| Show every shortcut | Win+Ctrl+/ (the key with ? on a US keyboard) | |

Shortcuts follow key positions, like on macOS: Win+[ is the key right of P on any keyboard layout (the accent key on a Spanish keyboard), and Settings shows each shortcut with your keyboard's own labels.

**Stacks**: Win+[ puts the focused window at the bottom of the column on its left, so the two share it, one above the other; Win+] does the same to the right. Pressed on a window that's already in a stack, they move it out into its own column on that side. Win+↑/↓ move between stacked windows, Win+Shift+↑/↓ reorder them, and Win+Ctrl+W turns the stack into tabs. While tiling is off, only Win+Ctrl+Shift+T stays with Glide; every other combo goes back to Windows.

### Tiled layout

Win+Ctrl+T switches the current workspace between the scrolling strip and a tiled layout, where every window fits on screen and nothing scrolls. Windows are tiled in order, like a dwindle or spiral layout: one window fills the screen, two share it as columns, and from the third on the last column splits again, top and bottom, then left and right, alternating. With two columns (the default) that gives `[1][2]`, `[1][2 over 3]`, `[1][2 over (3 | 4)]`…; with three, `[1][2][3]`, `[1][2][3 over 4]`, `[1][2][3 over (4 | 5)]`…. Win+Ctrl+G switches the workspace between 2 and 3 columns; by default Glide picks 3 on ultrawide monitors (at least twice as wide as tall) and 2 elsewhere.

- **Order**: tiles follow the strip's columns left to right. Switching to tiled gives every window of a stack its own column (a tabbed column stays one tile, with its tab bar on top); switching back gives every column the default width. The order and focus survive both ways. A new window becomes the next tile after the focused one.
- **Win+arrows** focus the tile next to the focused one in that direction, and **Win+Shift+arrows** swap it with that neighbour. Win+Home / End focus the first / last tile. Win+[ and ] do nothing.
- **Win+Ctrl+R** steps the split between the focused tile and the rest through ⅓, ½ and ⅔ (the last tile changes the split before it). Dragging a tile's edge moves the same split. The splits start over when a window opens or closes.
- **Win+Ctrl+F** zooms the focused tile to fill the screen until you press it again, or move focus away.
- Windows with a minimum width that doesn't fit their tile get that width, overlapping their neighbour if they have to.

### Rebinding

Open Settings from the tray menu, or launch Glide again from the Start menu while it runs. Under Shortcuts, click a shortcut and press the keys you want: any combo works, Win combos included. Esc cancels and Backspace removes it. Glide refuses combos that can't work (Win+L, which Windows handles before any app), warns about ones that shadow text editing, and tells you when a combo moves from another action.

## Settings

- **General**: turn tiling on or off, start at sign-in, the gap between windows, the slide animation, and the focus ring and its color.
- **Take over Windows shortcuts**: turn this off to give Windows back the combos Glide replaces, like FancyZones' "Override Windows Snap". Glide then only gets the shortcuts that don't clash.
- **App rules**: per app, always float, always tile or decide automatically, plus a column width and a workspace for new windows. Rules apply to open windows right away; a window you floated or tiled by hand with Win+Ctrl+V keeps your choice.
- **Restart as administrator**: Windows doesn't let a normal app move the windows of apps running as administrator, nor see keys while one is focused. Restart Glide as administrator from General to manage them too. Turning on "Start Glide when I sign in" while it runs as administrator makes it start that way every time (through a scheduled task, without a prompt).

Everything is stored in `%APPDATA%\Glide\settings.json` and applies as you change it. The file can also be edited by hand; Glide picks up the changes when it's saved:

```json
{
  "gap": 12,
  "animate": true,
  "defaultLayout": "Scrolling",
  "tiledColumns": "auto",
  "focusRing": true,
  "focusRingColor": "accent",
  "focusRingWidth": 4,
  "overrideWindowsShortcuts": true,
  "horizontalWheel": "Always",
  "trackpadSwipe": true,
  "rememberApps": true,
  "checkForUpdates": true,
  "language": "auto",
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

Action names are those in [`Action.cs`](src/Glide.Core/Action.cs). `focusRingColor` is `accent` or `#RRGGBB`; `horizontalWheel` is `Always`, `WithWin` or `Off`; `defaultLayout` is `Scrolling` or `Tiled` (any capitalization), for workspaces you haven't switched with Win+Ctrl+T; `tiledColumns` is `auto` (3 columns on ultrawide monitors, else 2), `2` or `3`, for tiled workspaces you haven't switched with Win+Ctrl+G. In rules, `mode` is `Automatic`, `Float` or `Tile`, `widthIndex` picks ⅓, ½, ⅔ or full (0–3), and `workspace` is zero-based.

## How it behaves

- Standard resizable app windows tile. Tool windows, owned windows and dialogs, fixed-size, cloaked and minimized windows are left alone, and so are windows of apps running as administrator (Windows doesn't let a normal app move them).
- New windows open right of the focused column and take focus. Minimized or hidden windows leave the strip and come back to the same spot.
- Off-screen columns wait as thin slivers at the screen edges; windows of other workspaces and inactive tabs wait in the bottom-right corner.
- Resizing a window with the mouse gives its column that width; dragging one snaps it back into place.
- Turning tiling off (Win+Ctrl+Shift+T or the tray menu) or quitting puts every window back on screen, including those on other workspaces. The layout is saved and comes back when tiling is turned on again, until Windows restarts.
- **Monitors**: each monitor has its own strip and its own nine workspaces. Shortcuts act on the monitor of the focused window (or the pointer, when nothing tiled has focus), new windows join the monitor they open on, and nothing peeks onto a neighbouring monitor. Win+Alt+←/→ move focus between monitors and Win+Alt+Shift+←/→ send the focused column over. Unplugging a monitor moves its windows to the primary one.
- **Drag and drop**: drop a window onto a tab bar to add it to those tabs, or onto another window to stack it into that column; drop it on another monitor to move it there. Floating windows can be dropped in too. While you drag, an outline in the focus ring color shows the column it would join, or a bar where it would go on the other monitor; no highlight means it will snap back.
- **Focus ring**: a colored ring around the focused window, for every app (Electron apps such as Discord included), with its color and thickness in Settings.
- **Overview** (Win+Tab): every workspace in use as a row of live window previews, to scale, with app icons; rows too long for the screen scroll (the wheel scrolls the row under the pointer). Arrows select, Enter or a click goes to the window (switching workspace if needed), typing filters by app name and title, Esc clears the filter and then closes.
- **Tabs** (Win+Ctrl+W): a column's windows become tabs, with a bar above the column. Win+↑/↓ or a click on a tab switches tabs; the bar never takes focus from the app.
- **Touchpad**: on a precision touchpad, three-finger swipes left and right glide along the strip and snap to a column when you lift. Windows uses those swipes to switch apps too, so set Windows' three-finger gestures to "Nothing" (Settings › Bluetooth & devices › Touchpad); Glide's Settings links there.
- **Mouse**: the horizontal wheel (tilting the wheel, or a thumb wheel like the MX Master's) glides along the strip and snaps to a column when you stop; Win + the vertical wheel does the same on any mouse. Settings › General › Mouse can limit the horizontal wheel to while Win is held, or give it back to apps.
- **Apps go back where you keep them**: Glide remembers each app's monitor, workspace, column width and place on the strip. When you reopen an app, its window opens there, between the same neighbours; after a restart, open windows are arranged the same way. App rules still come first, and Settings › General can turn this off or forget everything.
- **Updates**: Glide looks for a newer version on this repository's [releases](https://github.com/QuackByte/glide-windows/releases) twice a day and offers it from the tray icon. The installer is checked against the release's checksums, then Glide puts your windows back, updates and starts again. Turn it off while installing, or later under Settings › Updates (`checkForUpdates`); the check sends GitHub nothing but a request for the latest release, with Glide's version in the user agent.
- **If Glide crashes** or is ended from Task Manager, a small watchdog copy of Glide puts every window back on screen, and after a real crash it starts Glide again (at most once a minute), which re-tiles from the saved layout.
- **Workspaces**: switching or sending a column to another workspace shows the workspaces in use for a moment near the bottom of the screen.
- **Tray menu**: the tray icon shows the active workspace; click it for the menu. It lists the workspaces in use with how many windows each holds (pick one to go there), and under Shortcuts every action with its shortcut, to run it from there or to see them all on one page. When "Take over Windows shortcuts" is off, it says which shortcuts Windows keeps. `Glide.exe --quit` quits a running Glide.
- **Full screen**: a window that goes full screen (F11 in a browser, a video, a game) is left alone until it comes back, without the ring, dimming or tab bar over it, and the horizontal wheel and touchpad swipes go to it instead of the strip. Win + the wheel still glides the strip.
- **Windows' virtual desktops**: Glide tiles the desktop you're on. Windows on other desktops leave the strip while you're away and come back in the same order. Win+Ctrl+←/→ are Glide's by default; switch desktops from Task View, or turn off "Take over Windows shortcuts" to get them back.
- **Report a problem** (tray menu, or Settings › General): Glide puts its log and settings in a zip, with window titles taken out, shows it in Explorer and opens a new GitHub issue to describe the problem and attach the zip. Nothing is sent by itself.
- **Language**: Glide, its installer included, speaks English, Spanish, French, German, Japanese, Simplified Chinese, Korean and Brazilian Portuguese, following Windows' display language (Traditional Chinese stays English). Settings › General has a Language picker; `language` in settings.json chooses too (`auto`, `en`, `es`, `fr`, `de`, `ja`, `zh`, `ko` or `pt`). Either takes effect the next time Glide starts. What's New follows the language too; a release not yet translated shows in English.

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

### Smoke test

`tests\smoke\Smoke.ps1` drives a real Glide with its shortcuts and checks where windows land: tiling, column widths, stacking, workspaces, floating, and every window back on screen after quitting. Run it on a Windows PC or VM you're signed in to, and leave the keyboard alone for the minute it takes:

```powershell
powershell -ExecutionPolicy Bypass -File tests\smoke\Smoke.ps1 -Glide src\Glide\bin\ARM64\Debug\net9.0-windows10.0.19041.0\win-arm64\Glide.exe
```

It quits a running Glide, sets your Glide settings aside and puts them back afterwards, opens and closes three Notepad windows of its own, and starts your Glide again at the end.

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
