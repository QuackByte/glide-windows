<#
.SYNOPSIS
    Drives a real Glide with its shortcuts and checks where windows land: tiling, column widths,
    stacking, workspaces, floating, and putting every window back on quit.

.DESCRIPTION
    Run it on a Windows PC or VM, signed in, with nothing else needing the keyboard for a minute:

        powershell -ExecutionPolicy Bypass -File tests\smoke\Smoke.ps1 -Glide <path to Glide.exe>

    It quits any running Glide, sets your Glide settings aside (and puts them back after), opens
    three Notepad windows of its own, and closes them at the end. Other open windows get tiled
    too while it runs; the checks only look at its own. Exits with 1 if a check fails.
#>
param(
    [Parameter(Mandatory)] [string] $Glide
)

$ErrorActionPreference = 'Stop'
$Glide = (Resolve-Path $Glide).Path

# C# 5: what Windows PowerShell's Add-Type compiles.
Add-Type @"
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

public static class Smoke
{
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] struct KEYBDINPUT { public ushort Vk, Scan; public uint Flags, Time; public IntPtr Extra; }
    [StructLayout(LayoutKind.Sequential)] struct INPUT { public uint Type; public KEYBDINPUT Key; public long Padding; }
    delegate bool EnumProc(IntPtr hwnd, IntPtr data);

    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc callback, IntPtr data);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr hwnd);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr hwnd, StringBuilder name, int size);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetWindowText(IntPtr hwnd, StringBuilder text, int size);
    [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr hwnd, uint message, IntPtr w, IntPtr l);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern uint GetDpiForWindow(IntPtr hwnd);
    [DllImport("user32.dll")] static extern uint MapVirtualKey(uint code, uint type);
    [DllImport("user32.dll")] static extern uint SendInput(uint count, INPUT[] inputs, int size);
    [DllImport("user32.dll")] static extern bool SystemParametersInfo(uint action, uint param, out RECT rect, uint winIni);
    [DllImport("dwmapi.dll")] static extern int DwmGetWindowAttribute(IntPtr hwnd, int attribute, out RECT rect, int size);

    public static List<IntPtr> Windows(string className)
    {
        var found = new List<IntPtr>();
        EnumWindows(delegate(IntPtr hwnd, IntPtr data)
        {
            var name = new StringBuilder(256);
            GetClassName(hwnd, name, 256);
            if (IsWindowVisible(hwnd) && name.ToString() == className) found.Add(hwnd);
            return true;
        }, IntPtr.Zero);
        return found;
    }

    // The frame as seen, without invisible resize borders (DWMWA_EXTENDED_FRAME_BOUNDS).
    public static RECT Frame(IntPtr hwnd)
    {
        RECT r;
        DwmGetWindowAttribute(hwnd, 9, out r, Marshal.SizeOf(typeof(RECT)));
        return r;
    }

    public static string Describe(IntPtr hwnd)
    {
        var name = new StringBuilder(256);
        var title = new StringBuilder(256);
        GetClassName(hwnd, name, 256);
        GetWindowText(hwnd, title, 256);
        return hwnd + " " + name + " '" + title + "'";
    }

    public static RECT WorkArea()
    {
        RECT r;
        SystemParametersInfo(0x30 /* SPI_GETWORKAREA */, 0, out r, 0);
        return r;
    }

    // Presses the modifiers, taps the key at a physical position (its scan code, as Glide matches
    // keys, so any keyboard layout works), releases the modifiers.
    public static void Chord(ushort[] modifiers, ushort scanCode)
    {
        var inputs = new List<INPUT>();
        foreach (var m in modifiers) inputs.Add(Key(m, (ushort)MapVirtualKey(m, 0 /* VK_TO_VSC */), false, m == 0x5B));
        var vk = (ushort)MapVirtualKey(scanCode, 1 /* VSC_TO_VK */);
        inputs.Add(Key(vk, scanCode, false, false));
        inputs.Add(Key(vk, scanCode, true, false));
        for (var i = modifiers.Length - 1; i >= 0; i--) inputs.Add(Key(modifiers[i], (ushort)MapVirtualKey(modifiers[i], 0), true, modifiers[i] == 0x5B));
        SendInput((uint)inputs.Count, inputs.ToArray(), Marshal.SizeOf(typeof(INPUT)));
    }

    static INPUT Key(ushort vk, ushort scan, bool up, bool extended)
    {
        var input = new INPUT { Type = 1 };
        input.Key = new KEYBDINPUT { Vk = vk, Scan = scan, Flags = (up ? 2u : 0u) | (extended ? 1u : 0u) };
        return input;
    }
}
"@
[Smoke]::SetProcessDPIAware() | Out-Null

$failures = 0
function Check([string] $name, [scriptblock] $condition, [int] $seconds = 4) {
    # Glide moves windows asynchronously, and slides them; give them time to land.
    $deadline = (Get-Date).AddSeconds($seconds)
    do {
        if (& $condition) { Write-Host "  ok    $name"; return }
        Start-Sleep -Milliseconds 200
    } while ((Get-Date) -lt $deadline)
    Write-Host "  FAIL  $name (in front: $([Smoke]::Describe([Smoke]::GetForegroundWindow())))" -ForegroundColor Red
    $script:failures++
}

$Win = [uint16]0x5B; $Shift = [uint16]0x10; $Ctrl = [uint16]0x11
# Keys by position (scan code), named after the US key there.
$Key1 = 0x02; $Key2 = 0x03; $Key3 = 0x04; $KeyR = 0x13; $KeyV = 0x2F; $KeyLeftBracket = 0x1A; $KeyRightBracket = 0x1B
function Keys([uint16[]] $modifiers, [uint16] $scanCode) {
    [Smoke]::Chord($modifiers, $scanCode)
    Start-Sleep -Milliseconds 700
}

function Frame($hwnd) { [Smoke]::Frame($hwnd) }
function Inside($frame, $area) { $frame.Left -ge $area.Left - 2 -and $frame.Top -ge $area.Top - 2 -and $frame.Right -le $area.Right + 2 -and $frame.Bottom -le $area.Bottom + 2 }
# Windows of other workspaces wait at the desktop's bottom-right corner, a pixel showing.
function Away($hwnd) { (Frame $hwnd).Left -ge $area.Right - 2 }

$settingsDir = Join-Path $env:APPDATA 'Glide'
$aside = "$settingsDir.smoke-aside"
$running = Get-Process Glide -ErrorAction SilentlyContinue | Where-Object { $_.Path } | Select-Object -First 1 -ExpandProperty Path
$opened = @()

try {
    # A known starting point: no Glide running, default settings without animation or update checks.
    if ($running) { & $running --quit; Start-Sleep 3 }
    if (Test-Path $aside) { throw "$aside exists from an earlier run; put it back as $settingsDir first." }
    if (Test-Path $settingsDir) { Move-Item $settingsDir $aside }
    New-Item -ItemType Directory $settingsDir | Out-Null
    $version = ((Get-Item $Glide).VersionInfo.ProductVersion -split '\+')[0]
    @{ animate = $false; checkForUpdates = $false; rememberApps = $false; lastSeenVersion = $version } |
        ConvertTo-Json | Set-Content (Join-Path $settingsDir 'settings.json') -Encoding UTF8

    Write-Host "Starting $Glide"
    # Through Explorer, so Glide isn't a child of this script (and may take focus).
    explorer.exe $Glide
    Start-Sleep 4

    Write-Host 'Tiling'
    # One at a time, so $opened is in the order they opened.
    foreach ($n in 1..3) {
        $before = [Smoke]::Windows('Notepad')
        Start-Process notepad.exe
        $deadline = (Get-Date).AddSeconds(10)
        do { Start-Sleep -Milliseconds 300; $new = @([Smoke]::Windows('Notepad') | Where-Object { $before -notcontains $_ }) } while ($new.Count -eq 0 -and (Get-Date) -lt $deadline)
        $opened += $new
    }
    Check 'three new Notepad windows' { $opened.Count -eq 3 }
    $area = [Smoke]::WorkArea()
    $gap = [math]::Round(12 * [Smoke]::GetDpiForWindow($opened[0]) / 96)
    Check 'they are columns: full height, inside the gaps' {
        @($opened | Where-Object { $f = Frame $_; [math]::Abs($f.Top - ($area.Top + $gap)) -le 2 -and [math]::Abs($f.Bottom - ($area.Bottom - $gap)) -le 2 }).Count -eq 3
    }
    $newest = $opened[-1]
    Check 'the newest one has focus' { [Smoke]::GetForegroundWindow() -eq $newest }
    Check 'it is fully on screen' { Inside (Frame $newest) $area }

    Write-Host 'Column width'
    $width = (Frame $newest).Right - (Frame $newest).Left
    Keys @($Win, $Ctrl) $KeyR
    Check 'Win+Ctrl+R widens the column' { ((Frame $newest).Right - (Frame $newest).Left) -gt $width + 20 }
    Keys @($Win, $Ctrl, $Shift) $KeyR
    Check 'Win+Ctrl+Shift+R narrows it back' { [math]::Abs(((Frame $newest).Right - (Frame $newest).Left) - $width) -le 2 }

    Write-Host 'Stacking'
    $height = (Frame $newest).Bottom - (Frame $newest).Top
    Keys @($Win) $KeyLeftBracket
    Check 'Win+[ stacks it into the column on its left, at half height' {
        $f = Frame $newest
        $f.Bottom - $f.Top -lt $height * 0.6 -and @($opened | Where-Object { $_ -ne $newest -and [math]::Abs((Frame $_).Left - $f.Left) -le 2 }).Count -eq 1
    }
    Keys @($Win) $KeyRightBracket
    Check 'Win+] takes it out again' { [math]::Abs(((Frame $newest).Bottom - (Frame $newest).Top) - $height) -le 2 }

    Write-Host 'Workspaces'
    Keys @($Win, $Shift) $Key2
    Check 'Win+Shift+2 sends it away' { Away $newest }
    Keys @($Win) $Key2
    Check 'Win+2 shows it, focused' { (Inside (Frame $newest) $area) -and [Smoke]::GetForegroundWindow() -eq $newest }
    Check 'the others wait out of sight' { @($opened | Where-Object { $_ -ne $newest -and -not (Away $_) }).Count -eq 0 }
    Keys @($Win, $Shift) $Key1
    Keys @($Win) $Key1
    Check 'Win+Shift+1 and Win+1 bring it back with the others' { (Inside (Frame $newest) $area) -and [Smoke]::GetForegroundWindow() -eq $newest }

    Write-Host 'Floating'
    Keys @($Win, $Ctrl) $KeyV
    Check 'Win+Ctrl+V floats it, smaller and centered' {
        $f = Frame $newest
        $f.Bottom - $f.Top -lt $height - 20 -and [math]::Abs(($f.Left + $f.Right) / 2 - ($area.Left + $area.Right) / 2) -le 4
    }
    Keys @($Win, $Ctrl) $KeyV
    Check 'Win+Ctrl+V again tiles it' { [math]::Abs(((Frame $newest).Bottom - (Frame $newest).Top) - $height) -le 2 }

    Write-Host 'Quitting'
    # One window on another workspace when Glide quits.
    Keys @($Win, $Shift) $Key3
    & $Glide --quit
    Check 'Glide quits' { -not (Get-Process Glide -ErrorAction SilentlyContinue) } 10
    Check 'every window is back on screen, the one on workspace 3 too' { @($opened | Where-Object { -not (Inside (Frame $_) $area) }).Count -eq 0 }
}
finally {
    foreach ($hwnd in $opened) { [Smoke]::PostMessage($hwnd, 0x10 <# WM_CLOSE #>, [IntPtr]::Zero, [IntPtr]::Zero) | Out-Null }
    if (Get-Process Glide -ErrorAction SilentlyContinue) { & $Glide --quit; Start-Sleep 3 }
    if (Test-Path $aside) {
        Remove-Item $settingsDir -Recurse -Force -ErrorAction SilentlyContinue
        Move-Item $aside $settingsDir
    }
    if ($running) { explorer.exe $running }
}

if ($failures) {
    Write-Host "$failures check(s) failed" -ForegroundColor Red
    exit 1
}
Write-Host 'All checks passed' -ForegroundColor Green
