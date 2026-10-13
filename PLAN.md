# Plan

The goal is feature parity with Glide for macOS 0.1.0, with the same default shortcuts and behavior, built natively for Windows 11 (x64 and ARM64).

## Decision needed first: the stack

| Option | For | Against |
| --- | --- | --- |
| **C# + WinUI 3** (recommended) | Closest to Swift/SwiftUI to maintain. Native Windows 11 look (Mica, Fluent). Fast to build the settings window, overview, tab bars and on-screen display. Win32 through CsWin32; NativeAOT for a single exe. | Needs the .NET runtime unless built with NativeAOT. Unpackaged WinUI 3 deployment takes some setup. |
| Rust + windows-rs | What komorebi and GlazeWM use. One small exe, no runtime. | The UI (settings, overview) is harder and less native: egui, Slint or WebView2. |
| Swift on Windows | Shares GlideCore and its tests with the macOS app. | Windows UI from Swift is immature: raw Win32 or experimental WinUI bindings. |

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
| Carbon `RegisterEventHotKey` | `RegisterHotKey`, or a low-level keyboard hook for combos Windows reserves |
| ⌃⌥ default modifiers | Ctrl+Alt is AltGr on many keyboard layouts, so the Windows defaults likely move to Win+Alt or Win+Ctrl. Check against Windows' own Win+ shortcuts. |
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
