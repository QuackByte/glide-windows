# Plan

The goal is feature parity with Glide for macOS 0.1.0, with the same default shortcuts and behavior, built natively for Windows 11 (x64 and ARM64).

## Stack (decided)

**C# + WinUI 3:** .NET 9 with the Windows App SDK, unpackaged and self-contained. Win32 and DWM calls go through CsWin32. Targets x64 and ARM64.

## Keyboard: Win is Glide's modifier

Glide replaces the built-in window management the way FancyZones' "Override Windows Snap" does. `RegisterHotKey` refuses combos Windows reserves (Win+arrows and others), so shortcuts go through a low-level keyboard hook (`WH_KEYBOARD_LL`) that swallows Glide's combos before Windows sees them.

Proposed defaults, keeping the macOS shape (base modifier to focus, add Shift to move):

| Action | macOS | Windows | Replaces |
| --- | --- | --- | --- |
| Focus column left / right | ⌃⌥← / → | Win+← / → | Snap left/right |
| Focus window up / down | ⌃⌥↑ / ↓ | Win+↑ / ↓ | Maximize / restore-minimize |
| Focus first / last column | ⌃⌥Home / End | Win+Home / End | Minimize other windows (Home) |
| Move column / window | ⌃⌥⇧ + arrows | Win+Shift + arrows | Move to monitor, stretch vertically |
| Consume or expel left / right | ⌃⌥[ / ] | Win+[ / ] | (free) |
| Switch to workspace 1…9 | ⌃⌥1…9 | Win+1…9 | Open the Nth taskbar app |
| Send column to workspace 1…9 | ⌃⌥⇧1…9 | Win+Shift+1…9 | New instance of the Nth taskbar app |
| Previous / next workspace | ⌃⌥PgUp / PgDn | Win+Ctrl+← / → | Switch virtual desktop (Glide workspaces take their place) |
| Overview | ⌃⌥O | Win+Tab | Task View |
| Cycle width (backward) | ⌃⌥R (⇧) | Win+Ctrl+R (+Shift) | (free) |
| Toggle full width | ⌃⌥F | Win+Ctrl+F | Find computers (domain PCs only) |
| Center column | ⌃⌥C | Win+Ctrl+C | Color filters (off by default) |
| Toggle tabbed column | ⌃⌥W | Win+Ctrl+W | (free) |
| Toggle floating | ⌃⌥V | Win+Ctrl+V | Sound output flyout (Windows 11) |
| Enable or disable tiling | ⌃⌥⇧T | Win+Ctrl+Shift+T | (free) |

Left alone on purpose: Win alone (Start), Win+L, Win+D, Win+E, Win+R, Win+V, Win+S, Win+X, Win+Shift+S, Win+. (emoji), Win+Alt+… (Game Bar). Every shortcut stays rebindable, and there's a setting to give the replaced combos back to Windows (like FancyZones' toggle).

Things to handle:

- **Start menu:** releasing Win after a swallowed Win+key combo can open Start. Send a masking key event (`VK_NONAME`/0xFF) before Win goes up, like AutoHotkey's MenuMaskKey.
- **Win+L** is handled below any hook and can't be overridden (nor should it be).
- **Elevated windows:** while an app running as administrator has focus, the hook doesn't see keys, and UIPI stops Glide from moving admin windows. Offer "run as administrator", as PowerToys does.
- **Hook timeout:** Windows silently removes a hook whose callback takes longer than `LowLevelHooksTimeout`. The callback only matches and swallows the key; actions run on the UI thread.
- **Snap features:** offer to turn off Snap layouts and Snap Assist so they don't fight the strip.

## How the macOS pieces map to Windows

| Glide for macOS | Windows equivalent |
| --- | --- |
| GlideCore (strip, workspaces, overview selection, rules, snapshots) | Port as a pure library with the same unit tests (125 on macOS). |
| Accessibility API (AXObserver, AXUIElement) | `SetWinEventHook` (`EVENT_OBJECT_CREATE`/`DESTROY`/`SHOW`/`HIDE`, `EVENT_SYSTEM_FOREGROUND`, `EVENT_SYSTEM_MOVESIZESTART`/`END`, `EVENT_SYSTEM_MINIMIZESTART`/`END`), `EnumWindows` |
| Moving and resizing windows | `SetWindowPos`/`DeferWindowPos`. Use `DwmGetWindowAttribute(DWMWA_EXTENDED_FRAME_BOUNDS)` to correct for invisible resize borders. |
| Focus (`AXRaise`, `AXFrontmost`) | `SetForegroundWindow` (needs the Alt-key / `AllowSetForegroundWindow` workaround) |
| Window classification (dialogs, sheets, fixed size) | Window styles (`WS_EX_TOOLWINDOW`, `WS_THICKFRAME`, owner windows), cloaked check (`DWMWA_CLOAKED`), `WS_EX_APPWINDOW` |
| Parking off-screen columns as slivers | Windows allows fully off-screen windows, so park them completely or keep a sliver for consistency. |
| Hidden tabs and inactive workspaces (corner trick) | Move off-screen, or `ShowWindow(SW_HIDE)` (taskbar side effects to weigh) |
| Carbon `RegisterEventHotKey` | Low-level keyboard hook (see Keyboard above) |
| Focus ring overlay | Windows 11 `DWMWA_BORDER_COLOR` colors the real window border, so no overlay is needed. |
| Dimming | A click-through layered window (`WS_EX_LAYERED \| WS_EX_TRANSPARENT`) placed in z-order below the focused window |
| Tab bars, on-screen display, overview | WinUI windows. Overview previews via `DwmRegisterThumbnail` (live, no permission needed). |
| Three-finger swipe (MultitouchSupport) | Precision touchpad raw input (`RegisterRawInputDevices`, HID digitizer). Windows uses three-finger swipes too: same trade-off as on macOS. |
| Menu bar item | Notification-area (tray) icon with the workspace number |
| Open at login (`SMAppService`) | A Startup entry (`HKCU\...\Run`) or a startup task |
| Crash watchdog | The same design: a second copy of the process that restores windows if Glide exits uncleanly |
| UserDefaults | JSON settings file in `%APPDATA%\Glide` |
| Developer ID signing | Code signing certificate (or unsigned during development) |

## Milestones

1. **Engine:** port GlideCore and its tests.
2. **Core tiling:** window tracking, strip layout, default shortcuts, tray icon, enable/disable, restore windows on exit.
3. **Settings:** shortcut recorder, gaps, focus ring, rules.
4. **Workspaces, tabs, overview, on-screen display.**
5. **Trackpad, watchdog, open at login, release.**

## Testing

Develop against the Windows 11 ARM64 VM in Parallels on the Mac (`prlctl exec` for builds and scripted checks), the same way Glide for macOS was tested live.
