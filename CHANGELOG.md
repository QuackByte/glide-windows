# Changelog

What's new in each version of Glide for Windows. Glide shows the new entries once after it updates, and each release's notes on GitHub come from here, so write for people using Glide. Add an entry for every change worth knowing about under the next version before tagging it, and translate it in each file of `changelog/` (`CHANGELOG.es.md`, `CHANGELOG.fr.md` and so on, same versions, sections and links): Glide shows the notes in its own language, and a test checks they match this file. The GitHub notes stay English.

## 0.11.0 — 2026-10-20

### New

- **In the tiled layout, Win+Ctrl+F makes the whole workspace full screen.** Every window fills the screen, with the focused one on top, until you press Win+Ctrl+F again. Win+arrows move through the windows without leaving it, and new windows open full screen too.

## 0.10.2 — 2026-10-19

### Fixes

- **In the tiled layout, Win+Up and Win+Down switch tabs in a tab group before moving to the tile in that direction.** They step through the tabs of the focused group first, and once there are no more tabs that way, they move to the tile in that direction.
- **In the tiled layout, the tiles beside an app with a minimum width shrink to fit it instead of overlapping.** Glide learns each app's minimum width, and when a resize stops at that width, a message says why.

## 0.10.1 — 2026-10-19

### Fixes

- **In the tiled layout, Win+[ and Win+] make tab groups.** Win+[ adds the focused window to a tab group, a column of tabs, and Win+] moves a tab out into its own tile, as they already did when scrolling. Dragging a window onto a tab bar works as before.

## 0.10.0 — 2026-10-19

### New

- **Tiled is now a spiral layout.** Win+Ctrl+T switches a workspace to a layout where every window fits on screen: one window fills it, two share it as halves, and from the third on the last column splits again, alternating top and bottom with left and right. It uses 2 columns, or 3 with the new Win+Ctrl+G, which is also what Glide picks on ultrawide monitors. Switching back to scrolling keeps your windows in the same order.
- **Move around a tiled workspace by direction.** Win+arrows focus the tile next to the focused one, and Win+Shift+arrows swap it with that neighbour. Cycling width (Win+Ctrl+R) changes how the focused tile splits the room with the tiles after it, and Win+Ctrl+F zooms a tile to fill the screen until you press it again.
- **Choose how many columns tiled workspaces use** in Settings › General › Tiled columns: Automatic (3 on ultrawide monitors, else 2), 2 or 3.

### Changes

- **Sending a window to another workspace moves just that window.** Win+Shift+1…9 and Win+Ctrl+Shift+←/→ used to take the whole column along, and now only the focused window goes, as a column of its own, while you stay on the workspace you're on.

### Fixes

- **Brazilian Portuguese for the Layout setting.** Its description in Settings › General now reads in Brazilian Portuguese ("tela", "você") instead of European.

## 0.9.3 — 2026-10-19

### New

- **Tiled layout.** Win+Ctrl+T switches the current workspace between the scrolling strip and a tiled layout, where every column shares the screen so they all fit, without scrolling. Each workspace keeps its own layout, even after Glide restarts. Cycling a column's width (Win+Ctrl+R) gives it a bigger or smaller share, dragging its edge resizes it, and Win+Ctrl+F still makes it full width until you press it again.
- **Choose the layout new workspaces start with** in Settings › General › Layout: Scrolling, as before, or Tiled. Workspaces you've switched with Win+Ctrl+T keep their own.

## 0.9.2 — 2026-10-16

### New

- **What's New speaks your language too.** The notes Glide shows after an update, and under What's New, are now in the same language as the rest of Glide.

## 0.9.1 — 2026-10-16

### New

- **Glide speaks your language.** Glide is now in French, German, Japanese, Simplified Chinese, Korean and Brazilian Portuguese, as well as English and Spanish, installer included. It follows Windows' display language, or choose one under Settings › General › Language.

## 0.9.0 — 2026-10-15

### New

- **See where a dragged window will land.** While you drag a window, an outline in the focus ring color shows the column it would join, tab bar included. Over another monitor, a bar shows where it would go between that monitor's columns. No highlight means it will snap back.
- **A fuller tray menu.** It lists the workspaces in use with how many windows each holds; pick one to go there. Under **Shortcuts** is every action with its shortcut, to run it from the menu. When "Take over Windows shortcuts" is off, the menu also lists the shortcuts Windows keeps.
- **Every shortcut on one page.** Win+Ctrl+/ (the key with ? on a US keyboard) shows them all; press it again or Esc to close. It's also in the tray menu, under Shortcuts.
- **Report a Problem**, in the tray menu and in Settings › General. Glide puts its log and settings in a zip, with window titles taken out, and opens a new issue on GitHub for you to describe what happened and attach the zip. Nothing is sent by itself.
- **Glide habla español.** When Windows is in Spanish, Glide is too, installer included. What's New stays in English.

### Fixes

- **Full screen stays full screen.** A video, a game or F11 in a browser is no longer pulled back into its column. Glide leaves it alone, hides the focus ring, dimming and tab bar over it, and lets the horizontal wheel and touchpad swipes reach it.
- Switching to another of Windows' virtual desktops and back keeps your columns in the same order.
- Closing a window no longer pulls focus away from Glide's own windows, such as What's New or Settings.

## 0.8.1 — 2026-10-14

### Fixes

- Clicking **Continue** in the What's new window closed Glide. Closing a window now leaves Glide running.
- The tray icon shows every workspace in use: a 3×3 grid of the nine workspaces, the ones in use lit and the active one brightest. With only one in use, it shows the number as before.

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
