# Changelog

What's new in each version of Glide for Windows. Glide shows the new entries once after it updates, and each release's notes on GitHub come from here, so write for people using Glide. Add an entry for every change worth knowing about under the next version before tagging it.

## 0.8.0 — 2026-10-14

### New

- **Dim unfocused windows.** The windows behind the focused one on its monitor go darker, so the focused window stands out. Off by default; turn it on, and set how dark, in Settings › Appearance.
- **Every workspace in use is listed.** The tray icon's tooltip and menu show all the workspaces with windows on them. The icon still shows the active one.
- **Workspace numbers on screen when switching** can be turned off in Settings › Appearance.
- **Install updates automatically.** Off by default. Turn it on in Settings › Updates, and Glide installs a new version as soon as it finds one, then starts again.

### Changes

- Releases aren't code-signed yet, so Windows SmartScreen may warn the first time you run Glide: choose **More info › Run anyway**.
- The installer's privacy policy no longer links to a code-signing policy.

## 0.7.0 — 2026-10-14

### New

- **Glide keeps itself up to date.** Twice a day it looks for a new version and offers it from the tray icon. Every download is checked before it installs, and Glide puts your windows back, updates and starts again. Turn it off in Settings › Updates, or while installing.
- **What's new.** After an update, Glide shows what changed, like this. Find it again in the tray menu.
- **Apps go back where you keep them.** Glide remembers each app's monitor, workspace, column width and place on the strip. A reopened app's window opens there, and after a restart your open windows are arranged the same way. Turn it off or forget everything in Settings › General.
- **Glide is open source**, under the MIT license: [github.com/QuackByte/glide-windows](https://github.com/QuackByte/glide-windows).

### Changes

- The installer shows Glide's privacy policy and lets you choose whether Glide checks for updates.

## 0.6.0 — 2026-10-14

### New

- **App icons** next to window titles in the overview and in tab bars.
- **The overview scrolls** when a strip is too long for the screen, instead of shrinking tiles until they're unreadable. The wheel scrolls the row under the pointer.

### Fixes

- Tab bars slide with their column instead of jumping ahead of it.
- After stacking or unstacking, the focus ring could appear where the window used to be. It now follows the window.

## 0.5.0 — 2026-10-14

### New

- **Multiple monitors**: each monitor has its own strip and nine workspaces. Win+Alt+←/→ move focus between monitors; Win+Alt+Shift+←/→ send the focused column over.
- **Drag and drop**: drop a window on a tab bar to add it to the tabs, on another window to stack it there, or on another monitor to move it.
- **A focus ring for every app**, Electron apps included, with a thickness you can set.
- **Three-finger touchpad swipes** glide along the strip.
- "Start Glide When I Sign In" in the tray menu.

## 0.4.1 — 2026-10-14

The first release of Glide for Windows.
