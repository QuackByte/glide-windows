using System.Diagnostics;
using Glide.Core;
using Windows.Win32;
using Windows.Win32.Foundation;

namespace Glide.Platform;

/// <summary>
/// Tracks app windows, keeps them on the strip, and applies the layout. Lives on the UI thread:
/// window events, shortcuts and timers all arrive there.
/// </summary>
internal sealed class WindowManager : IDisposable
{
    private const nuint AnimationTimer = 1, SettleTimer = 2;
    private static readonly string snapshotPath = Path.Combine(Settings.Directory, "layout.json");

    private readonly MessageWindow window;
    private Settings settings;
    private AppRules rules;
    private WinEventHooks? events;
    private WorkspaceSet spaces = new(default);

    /// <summary>Where each tiled window was last sent (visible frame, screen pixels).</summary>
    private readonly Dictionary<WindowId, Rect> placed = [];
    private readonly Dictionary<WindowId, Borders> borders = [];
    /// <summary>Windows that left (minimized, hidden, cloaked) and where to put them back.</summary>
    private readonly Dictionary<WindowId, WorkspaceSet.Departure> departures = [];
    private readonly HashSet<WindowId> userFloating = [], userTiled = [];
    /// <summary>Windows waiting off screen as hidden tabs or on inactive workspaces.</summary>
    private readonly HashSet<WindowId> hidden = [];

    private Dictionary<WindowId, (Rect From, Rect To)> motions = [];
    private long animationStart;
    private bool timerResolutionRaised;

    /// <summary>Glide just focused this window; ignore other foreground changes until the deadline.</summary>
    private (WindowId Window, long Until)? focusLock;
    private long workspaceSwitchLockUntil;
    /// <summary>The window whose border Glide colored, and the color.</summary>
    private (WindowId Window, uint Color)? ringed;

    public WindowManager(MessageWindow window, Settings settings)
    {
        this.window = window;
        this.settings = settings;
        rules = new AppRules(settings.Rules);
    }

    public bool IsEnabled { get; private set; }
    public int ActiveWorkspaceNumber => spaces.ActiveIndex + 1;

    /// <summary>Where tab bars go now (empty while tiling is off).</summary>
    public event Action<IReadOnlyList<TabBar>>? TabBarsChanged;

    /// <summary>The workspace changed or a column went to another one: for the on-screen display.</summary>
    public event Action<WorkspaceAnnouncement>? WorkspaceAnnounced;

    /// <summary>The workspace or the enabled state changed.</summary>
    public event Action? StateChanged;

    public void ApplySettings(Settings newSettings)
    {
        var rulesChanged = !newSettings.Rules.SequenceEqual(settings.Rules);
        settings = newSettings;
        rules = new AppRules(settings.Rules);
        if (!IsEnabled) return;
        UpdateDisplay();
        placed.Clear();
        if (rulesChanged) ReconcileRules();
        Layout(animate: false);
        UpdateFocusRing();
    }

    /// <summary>
    /// Applies changed rules to windows already open: tiled windows a rule now floats leave the
    /// strip, and open windows a rule now tiles join it. Choices made by hand are kept.
    /// </summary>
    private void ReconcileRules()
    {
        foreach (var id in spaces.AllWindows)
        {
            var hwnd = Native.Hwnd(id);
            if (!Native.Exists(hwnd) || ShouldTile(hwnd)) continue;
            Float(id, hwnd);
            Log.Info($"A rule floats {Describe(id)}");
        }
        foreach (var hwnd in Native.TopLevelWindows())
        {
            var id = Native.Id(hwnd);
            if (spaces.IndexOf(id) is not null || departures.ContainsKey(id) || !ShouldTile(hwnd, out var rule)) continue;
            spaces.Insert(id, rule?.WidthIndex ?? settings.DefaultWidthIndex(), rule?.Workspace, focus: false);
            Log.Info($"A rule tiles {Describe(id)}");
        }
    }

    /// <summary>Takes a window off the strip and puts it, smaller, in the middle of the screen.</summary>
    private void Float(WindowId id, HWND hwnd)
    {
        spaces.Remove(id);
        placed.Remove(id);
        hidden.Remove(id);
        motions.Remove(id);
        Native.SetVisibleFrame(hwnd, FloatingFrame(hwnd), Native.BordersOf(hwnd));
    }

    /// <summary>
    /// Colors the border of the focused window when it's tiled on the active workspace, and puts
    /// back the border of the one before.
    /// </summary>
    private void UpdateFocusRing()
    {
        (WindowId Window, uint Color)? target = null;
        if (IsEnabled && settings.FocusRing)
        {
            var foreground = Native.Id(Native.Foreground);
            if (spaces.Active.Strip.Contains(foreground)) target = (foreground, Native.ParseColor(settings.FocusRingColor));
        }
        RefreshTabBars();
        if (target == ringed) return;
        if (ringed is var (old, _) && Native.Exists(Native.Hwnd(old))) Native.SetBorderColor(Native.Hwnd(old), null);
        if (target is var (window, color)) Native.SetBorderColor(Native.Hwnd(window), color);
        ringed = target;
    }

    // Enabling and disabling

    public void Enable()
    {
        if (IsEnabled) return;
        IsEnabled = true;
        spaces = new WorkspaceSet(default);
        UpdateDisplay();
        events = new WinEventHooks(OnWinEvent);

        var snapshot = LoadSnapshot();
        if (snapshot is not null)
        {
            userFloating.UnionWith(snapshot.UserFloating.Where(w => Native.Exists(Native.Hwnd(w))));
            userTiled.UnionWith(snapshot.UserTiled.Where(w => Native.Exists(Native.Hwnd(w))));
        }

        // Windows join left to right in their current on-screen order.
        var foreground = Native.Id(Native.Foreground);
        var adopted = Native.TopLevelWindows()
            .Where(ShouldTile)
            .Select(hwnd => (Id: Native.Id(hwnd), Frame: Native.VisibleFrame(hwnd) ?? default))
            .OrderBy(w => w.Frame.X).ThenBy(w => w.Frame.Y)
            .Select(w => w.Id)
            .ToList();

        if (snapshot is not null)
        {
            var (strips, active) = snapshot.RestoreWorkspaces(adopted, settings.DefaultWidthIndex(), spaces.Settings.WidthPresets.Count);
            spaces.Restore(strips, active);
        }
        else
        {
            foreach (var id in adopted) spaces.Active.Strip.Insert(id, settings.DefaultWidthIndex(), at: spaces.Active.Strip.Columns.Count, focus: false);
        }
        if (spaces.IndexOf(foreground) is int workspace)
        {
            spaces.Activate(workspace);
            spaces.Active.Strip.Focus(foreground);
        }
        spaces.Active.EnsureFocusedVisible();
        Log.Info($"Tiling on: {spaces.AllWindows.Count} windows, workspace {ActiveWorkspaceNumber}: {spaces.Active.Strip}");
        Layout();
        UpdateFocusRing();
        StateChanged?.Invoke();
    }

    /// <summary>Stops tiling and moves every window somewhere fully on screen, hidden workspaces included.</summary>
    public void Disable()
    {
        if (!IsEnabled) return;
        FinishAnimations();
        SaveSnapshot();
        IsEnabled = false;
        UpdateFocusRing();
        var frames = spaces.AllWindows
            .Where(id => Native.Exists(Native.Hwnd(id)))
            .Select(id => (id, placed.TryGetValue(id, out var frame) ? frame : Native.VisibleFrame(Native.Hwnd(id)) ?? default))
            .ToList();
        var restored = RestorePlanner.RestoredFrames(frames, spaces.Viewport, inset: spaces.Settings.Gap);
        foreach (var (id, frame) in restored)
            Native.SetVisibleFrame(Native.Hwnd(id), frame, borders.GetValueOrDefault(id));
        Log.Info($"Tiling off: restored {restored.Count} windows");

        events?.Dispose();
        events = null;
        window.StopTimer(SettleTimer);
        spaces = new WorkspaceSet(default);
        placed.Clear();
        borders.Clear();
        departures.Clear();
        userFloating.Clear();
        userTiled.Clear();
        hidden.Clear();
        StateChanged?.Invoke();
    }

    public void Toggle()
    {
        if (IsEnabled) Disable();
        else Enable();
    }

    // Shortcuts

    public void Perform(GlideAction action)
    {
        if (action == GlideAction.ToggleTiling)
        {
            Toggle();
            return;
        }
        if (!IsEnabled) return;
        Log.Debug($"Action {action}");
        SyncFocusFromForeground();

        if (action.WorkspaceCommand() is var (target, movesColumn))
        {
            PerformWorkspace(target, movesColumn);
            return;
        }
        switch (action)
        {
            case GlideAction.ToggleFloating:
                ToggleFloating();
                return;
            case GlideAction.ToggleOverview:
                // Milestone 4.
                return;
        }
        if (!spaces.Active.Perform(action)) return;
        Layout();
        FocusActive();
    }

    /// <summary>The user may have clicked a window since the last event; act on the one they see focused.</summary>
    private void SyncFocusFromForeground()
    {
        var foreground = Native.Id(Native.Foreground);
        if (spaces.Active.Strip.Contains(foreground)) spaces.Active.Strip.Focus(foreground);
    }

    private void PerformWorkspace(WorkspaceTarget target, bool movesColumn)
    {
        if (spaces.Resolve(target) is not int index) return;
        if (movesColumn)
        {
            if (!spaces.MoveFocusedColumn(index)) return;
            Log.Info($"Moved column to workspace {index + 1}");
            Layout();
            FocusActive();
            Announce($"Column sent to workspace {index + 1}");
        }
        else
        {
            SwitchWorkspace(index, focusing: null);
        }
    }

    private void SwitchWorkspace(int index, WindowId? focusing)
    {
        if (!spaces.Activate(index)) return;
        workspaceSwitchLockUntil = Deadline(0.6);
        FinishAnimations();
        if (focusing is { } target)
        {
            spaces.Active.Strip.Focus(target);
            spaces.Active.EnsureFocusedVisible();
        }
        Layout();
        FocusActive();
        Log.Info($"Workspace {index + 1}: {spaces.Active.Strip}");
        StateChanged?.Invoke();
        Announce(spaces.Active.Strip.IsEmpty ? $"Workspace {index + 1} is empty" : $"Workspace {index + 1}");
    }

    private void Announce(string message) =>
        WorkspaceAnnounced?.Invoke(new WorkspaceAnnouncement(
            [.. spaces.Occupied.Append(spaces.ActiveIndex).Distinct().Order().Select(i => (i + 1, i == spaces.ActiveIndex))], message));

    // Overview and tabs

    /// <summary>Everything the overview shows, taken when it opens; null while tiling is off.</summary>
    public OverviewData? MakeOverview()
    {
        if (!IsEnabled) return null;
        var titles = new Dictionary<WindowId, string>();
        var search = new Dictionary<WindowId, string>();
        foreach (var id in spaces.AllWindows)
        {
            var hwnd = Native.Hwnd(id);
            var title = Native.Title(hwnd);
            titles[id] = title;
            search[id] = $"{Native.AppDescription(Native.ProcessId(hwnd))} {title}";
        }
        return new OverviewData(new Overview(spaces), titles, search, spaces.Viewport, spaces.ActiveIndex);
    }

    /// <summary>Shows and focuses a tiled window, switching workspace if needed (overview, tab bars).</summary>
    public void GoTo(WindowId id)
    {
        if (!IsEnabled || spaces.IndexOf(id) is not int workspace) return;
        if (workspace != spaces.ActiveIndex)
        {
            SwitchWorkspace(workspace, focusing: id);
            return;
        }
        spaces.Active.Strip.Focus(id);
        spaces.Active.EnsureFocusedVisible();
        Layout();
        FocusActive();
    }

    private string lastTabBars = "";

    /// <summary>Tells the app where tab bars go, after windows have reached their places.</summary>
    private void RefreshTabBars()
    {
        var bars = new List<TabBar>();
        if (IsEnabled)
        {
            var foreground = Native.Id(Native.Foreground);
            foreach (var bar in spaces.Active.TabBars())
            {
                bars.Add(new TabBar(bar.Frame, [.. bar.Windows.Select(w => (w, Native.Title(Native.Hwnd(w))))], bar.ActiveIndex,
                                    bar.Windows.Contains(foreground)));
            }
        }
        var key = string.Join("|", bars.Select(b => $"{b.Frame}{b.ActiveIndex}{b.IsFocused}{string.Join(",", b.Tabs)}"));
        if (key == lastTabBars) return;
        lastTabBars = key;
        TabBarsChanged?.Invoke(bars);
    }

    private void ToggleFloating()
    {
        var hwnd = Native.Foreground;
        var id = Native.Id(hwnd);
        if (spaces.IndexOf(id) is not null)
        {
            userTiled.Remove(id);
            userFloating.Add(id);
            Float(id, hwnd);
            Layout();
            Native.Focus(hwnd);
        }
        else
        {
            var facts = Native.Inspect(hwnd);
            if (!facts.IsCandidate || !facts.CanTile) return;
            userFloating.Remove(id);
            userTiled.Add(id);
            spaces.Insert(id, settings.DefaultWidthIndex(), focus: true);
            Layout();
        }
    }

    private Rect FloatingFrame(HWND hwnd)
    {
        var viewport = spaces.Viewport;
        var current = Native.VisibleFrame(hwnd) ?? new Rect(0, 0, viewport.Width / 2, viewport.Height / 2);
        var width = Math.Min(current.Width, viewport.Width * 0.7);
        var height = Math.Min(current.Height, viewport.Height * 0.7);
        return new Rect(Math.Round(viewport.MidX - width / 2), Math.Round(viewport.MidY - height / 2), width, height);
    }

    private void FocusActive()
    {
        if (spaces.Active.Strip.FocusedWindow is { } focused)
        {
            focusLock = (focused, Deadline(0.4));
            Native.Focus(Native.Hwnd(focused));
        }
        else
        {
            focusLock = (default, Deadline(0.4));
            Native.FocusDesktop();
        }
    }

    // Window events

    private void OnWinEvent(uint @event, HWND hwnd)
    {
        if (!IsEnabled) return;
        var id = Native.Id(hwnd);
        switch (@event)
        {
            case Win32.EVENT_SYSTEM_FOREGROUND:
                OnForeground(id, hwnd);
                break;
            case Win32.EVENT_OBJECT_SHOW or Win32.EVENT_OBJECT_UNCLOAKED or Win32.EVENT_SYSTEM_MINIMIZEEND:
                Appear(id, hwnd, focus: @event == Win32.EVENT_SYSTEM_MINIMIZEEND || Native.Foreground == hwnd);
                break;
            case Win32.EVENT_OBJECT_HIDE or Win32.EVENT_OBJECT_CLOAKED or Win32.EVENT_SYSTEM_MINIMIZESTART:
                Depart(id);
                break;
            case Win32.EVENT_OBJECT_DESTROY:
                Forget(id);
                break;
            case Win32.EVENT_SYSTEM_MOVESIZEEND:
                OnUserMoveOrResize(id, hwnd);
                break;
        }
        UpdateFocusRing();
    }

    private void OnForeground(WindowId id, HWND hwnd)
    {
        if (focusLock is var (locked, until) && Stopwatch.GetTimestamp() < until && locked != id) return;
        if (spaces.IndexOf(id) is not int workspace)
        {
            // Some windows only become tileable after they first show (a title arrives late).
            Appear(id, hwnd, focus: true);
            return;
        }
        if (workspace != spaces.ActiveIndex)
        {
            // Alt+Tab, the taskbar or a notification focused a window on another workspace.
            if (Stopwatch.GetTimestamp() < workspaceSwitchLockUntil) return;
            SwitchWorkspace(workspace, focusing: id);
            return;
        }
        spaces.Active.Strip.Focus(id);
        spaces.Active.EnsureFocusedVisible();
        Layout();
    }

    private void Appear(WindowId id, HWND hwnd, bool focus)
    {
        if (spaces.IndexOf(id) is not null) return;
        if (departures.TryGetValue(id, out var departure))
        {
            var facts = Native.Inspect(hwnd);
            if (!facts.IsCandidate) return;
            departures.Remove(id);
            var workspace = spaces.Rejoin(id, departure, focus);
            Log.Debug($"Returned {Describe(id)} to workspace {workspace + 1}");
            Layout();
            return;
        }
        if (!ShouldTile(hwnd, out var rule)) return;
        var target = spaces.Insert(id, rule?.WidthIndex ?? settings.DefaultWidthIndex(), rule?.Workspace, focus);
        Log.Info($"Tiling {Describe(id)} on workspace {target + 1}: {spaces.Workspaces[target].Strip}");
        Layout();
    }

    private void Depart(WindowId id)
    {
        if (spaces.DepartureOf(id) is not { } departure) return;
        var wasFocused = spaces.Active.Strip.FocusedWindow == id;
        departures[id] = departure;
        spaces.Remove(id);
        placed.Remove(id);
        hidden.Remove(id);
        Log.Debug($"{Describe(id)} left: {spaces.Active.Strip}");
        Layout();
        // Windows would otherwise activate whatever is next in z-order, maybe on another workspace.
        if (wasFocused) FocusActive();
    }

    private void Forget(WindowId id)
    {
        departures.Remove(id);
        userFloating.Remove(id);
        userTiled.Remove(id);
        if (spaces.IndexOf(id) is null) return;
        var wasFocused = spaces.Active.Strip.FocusedWindow == id;
        spaces.Remove(id);
        placed.Remove(id);
        borders.Remove(id);
        hidden.Remove(id);
        motions.Remove(id);
        Log.Debug($"Window {id} closed: {spaces.Active.Strip}");
        Layout();
        if (wasFocused) FocusActive();
    }

    /// <summary>
    /// A window the user dragged snaps back to its slot. One the user resized keeps its new width:
    /// its column takes that width and the strip reflows around it.
    /// </summary>
    private void OnUserMoveOrResize(WindowId id, HWND hwnd)
    {
        if (spaces.IndexOf(id) is not int workspace || Native.VisibleFrame(hwnd) is not { } actual) return;
        var strip = spaces.Workspaces[workspace].Strip;
        if (placed.TryGetValue(id, out var applied) && Math.Abs(actual.Width - applied.Width) > 2 && strip.Location(id) is (int column, _))
        {
            strip.SetCustomWidth(actual.Width, id);
            foreach (var sibling in strip.Columns[column].Windows) placed.Remove(sibling);
            spaces.Workspaces[workspace].EnsureFocusedVisible();
            Log.Debug($"Adopted width {actual.Width} for {Describe(id)}");
        }
        placed[id] = actual;
        Layout();
    }

    private bool ShouldTile(HWND hwnd) => ShouldTile(hwnd, out _);

    private bool ShouldTile(HWND hwnd, out AppRule? rule)
    {
        rule = null;
        var facts = Native.Inspect(hwnd);
        if (!facts.IsCandidate) return false;
        var id = Native.Id(hwnd);
        rule = rules.Rule(facts.AppId);
        return AppRules.ShouldTile(rule, facts.ClassifiedTileable, facts.CanTile, userFloating.Contains(id), userTiled.Contains(id));
    }

    // Layout

    public void UpdateDisplay()
    {
        var (workArea, scale) = Native.PrimaryDisplay();
        var standard = LayoutSettings.Standard;
        spaces.Settings = new LayoutSettings(
            gap: Math.Round(settings.Gap * scale), sliver: Math.Round(standard.Sliver * scale), widthPresets: standard.WidthPresets,
            defaultWidthIndex: standard.DefaultWidthIndex, tabBarHeight: Math.Round(standard.TabBarHeight * scale),
            tabBarSpacing: Math.Round(standard.TabBarSpacing * scale));
        spaces.Viewport = workArea;
        foreach (var workspace in spaces.Workspaces) workspace.SettleScroll();
    }

    public void OnDisplayChanged()
    {
        if (!IsEnabled) return;
        UpdateDisplay();
        placed.Clear();
        Layout(animate: false);
    }

    /// <summary>Sends every tiled window to its slot; hidden ones wait in the bottom-right corner.</summary>
    private void Layout(bool animate = true, int pass = 0)
    {
        if (!IsEnabled) return;
        animate &= settings.Animate;
        var progress = AnimationProgress();
        var viewport = spaces.Viewport;
        var next = new Dictionary<WindowId, (Rect From, Rect To)>();

        for (var index = 0; index < spaces.Workspaces.Count; index++)
        {
            var workspace = spaces.Workspaces[index];
            var active = index == spaces.ActiveIndex;
            foreach (var placement in workspace.Placements())
            {
                var id = placement.Window;
                var hwnd = Native.Hwnd(id);
                var isHidden = !active || placement.Visibility == Visibility.HiddenTab;
                var to = active ? placement.Frame : new Rect(workspace.HiddenOrigin, placement.Frame.Size);
                var wasHidden = isHidden ? !hidden.Add(id) : hidden.Remove(id);
                if (placed.TryGetValue(id, out var last) && last == to && !motions.ContainsKey(id)) continue;

                var from = motions.TryGetValue(id, out var motion) ? FrameAnimation.Frame(motion.From, motion.To, progress)
                    : placed.TryGetValue(id, out last) ? last : Native.VisibleFrame(hwnd);
                Native.Unmaximize(hwnd);
                var windowBorders = borders[id] = Native.BordersOf(hwnd);
                placed[id] = to;
                // Hidden windows jump; tabs switch in place rather than sliding in from the corner.
                if (animate && !isHidden && !wasHidden && from is { } start && FrameAnimation.ShouldAnimate(start, to, viewport))
                {
                    next[id] = (start, to);
                }
                else
                {
                    Native.SetVisibleFrame(hwnd, to, windowBorders);
                }
            }
        }

        motions = next;
        if (motions.Count > 0)
        {
            animationStart = Stopwatch.GetTimestamp();
            StartAnimationTimer();
            AnimationTick();
        }
        else
        {
            StopAnimationTimer();
            RefreshTabBars();
        }
        // Check where windows landed once they've had time to get there.
        window.StopTimer(SettleTimer);
        window.StartTimer(SettleTimer, (uint)(FrameAnimation.DefaultDuration * 1000) + 150, () =>
        {
            window.StopTimer(SettleTimer);
            Settle(pass);
        });
    }

    /// <summary>
    /// Learns minimum widths from apps that refuse to shrink (so the strip makes room) and nudges
    /// windows that didn't land where they were sent.
    /// </summary>
    private void Settle(int pass)
    {
        if (!IsEnabled || motions.Count > 0) return;
        var workspace = spaces.Active;
        var learned = false;
        foreach (var placement in workspace.Placements())
        {
            if (placement.Visibility != Visibility.Visible) continue;
            var hwnd = Native.Hwnd(placement.Window);
            if (Native.VisibleFrame(hwnd) is not { } actual) continue;
            var frame = placement.Frame;
            // A snapped window's size is Windows' doing, not a minimum the app insists on.
            if (Native.IsSnapped(hwnd))
            {
                Native.Unmaximize(hwnd);
                Native.SetVisibleFrame(hwnd, frame, borders[placement.Window] = Native.BordersOf(hwnd));
            }
            else if (actual.Width > frame.Width + 2 && actual.Width > workspace.MinimumWidths.GetValueOrDefault(placement.Window))
            {
                workspace.MinimumWidths[placement.Window] = actual.Width;
                learned = true;
            }
            else if (Math.Abs(actual.X - frame.X) > 2 || Math.Abs(actual.Y - frame.Y) > 2 || Math.Abs(actual.Width - frame.Width) > 2)
            {
                Native.SetVisibleFrame(hwnd, frame, borders[placement.Window] = Native.BordersOf(hwnd));
            }
        }
        if (learned && pass < 2)
        {
            Log.Debug($"Learned minimum widths: {string.Join(", ", workspace.MinimumWidths.Select(p => $"{p.Key}={p.Value}"))}");
            workspace.EnsureFocusedVisible();
            Layout(animate: true, pass + 1);
        }
    }

    private double AnimationProgress() =>
        motions.Count == 0 ? 1 : Stopwatch.GetElapsedTime(animationStart).TotalSeconds / FrameAnimation.DefaultDuration;

    private void AnimationTick()
    {
        var progress = AnimationProgress();
        foreach (var (id, (from, to)) in motions)
            Native.SetVisibleFrame(Native.Hwnd(id), FrameAnimation.Frame(from, to, progress), borders.GetValueOrDefault(id));
        if (progress >= 1)
        {
            motions = [];
            StopAnimationTimer();
            RefreshTabBars();
        }
    }

    private void FinishAnimations()
    {
        foreach (var (id, (_, to)) in motions) Native.SetVisibleFrame(Native.Hwnd(id), to, borders.GetValueOrDefault(id));
        motions = [];
        StopAnimationTimer();
    }

    private void StartAnimationTimer()
    {
        // Timers tick every 15.6 ms by default; ask for 1 ms resolution while animating.
        if (!timerResolutionRaised) timerResolutionRaised = PInvoke.timeBeginPeriod(1) == 0;
        window.StartTimer(AnimationTimer, 8, AnimationTick);
    }

    private void StopAnimationTimer()
    {
        window.StopTimer(AnimationTimer);
        if (timerResolutionRaised)
        {
            PInvoke.timeEndPeriod(1);
            timerResolutionRaised = false;
        }
    }

    // Persistence

    /// <summary>Window handles are reused after a restart, so a snapshot is only valid within one boot.</summary>
    private static DateTimeOffset BootTime => DateTimeOffset.UtcNow - TimeSpan.FromMilliseconds(Environment.TickCount64);

    private void SaveSnapshot()
    {
        try
        {
            Directory.CreateDirectory(Settings.Directory);
            var snapshot = new StripSnapshot(spaces, userFloating, userTiled, BootTime);
            File.WriteAllText(snapshotPath, snapshot.ToJson());
        }
        catch (Exception e)
        {
            Log.Error("Couldn't save the layout", e);
        }
    }

    private static StripSnapshot? LoadSnapshot()
    {
        try
        {
            if (!File.Exists(snapshotPath)) return null;
            var snapshot = StripSnapshot.FromJson(File.ReadAllText(snapshotPath));
            return snapshot is not null && Math.Abs((snapshot.BootTime - BootTime).TotalSeconds) < 60 ? snapshot : null;
        }
        catch (Exception e)
        {
            Log.Error("Couldn't read the saved layout", e);
            return null;
        }
    }

    private static string Describe(WindowId id)
    {
        var hwnd = Native.Hwnd(id);
        return $"{id} \"{Native.Title(hwnd)}\" ({Native.ExecutableName(Native.ProcessId(hwnd))})";
    }

    private static long Deadline(double seconds) => Stopwatch.GetTimestamp() + (long)(seconds * Stopwatch.Frequency);

    public void Dispose()
    {
        Disable();
        StopAnimationTimer();
    }
}

internal static class SettingsExtensions
{
    public static int DefaultWidthIndex(this Settings _) => LayoutSettings.Standard.DefaultWidthIndex;
}

/// <summary>A tab bar above a tabbed column, in screen pixels.</summary>
/// <param name="IsFocused">The column holds the focused window, so its active tab is highlighted.</param>
public sealed record TabBar(Rect Frame, IReadOnlyList<(WindowId Window, string Title)> Tabs, int ActiveIndex, bool IsFocused)
{
    public WindowId ActiveWindow => Tabs[ActiveIndex].Window;
}

/// <summary>The workspaces in use (and the active one), and what just happened.</summary>
public sealed record WorkspaceAnnouncement(IReadOnlyList<(int Number, bool IsActive)> Workspaces, string Message);

/// <summary>What the overview shows: the layout, window titles, what typing filters on, and the screen.</summary>
public sealed record OverviewData(Overview Overview, IReadOnlyDictionary<WindowId, string> Titles,
                                  IReadOnlyDictionary<WindowId, string> SearchText, Rect Viewport, int ActiveWorkspace);
