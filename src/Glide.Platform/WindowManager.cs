using System.Diagnostics;
using System.Text.Json;
using Glide.Core;
using Windows.Win32;
using Windows.Win32.Foundation;

namespace Glide.Platform;

/// <summary>
/// Tracks app windows, keeps them on the strips, and applies the layout. Lives on the UI thread:
/// window events, shortcuts and timers all arrive there.
/// </summary>
/// <remarks>
/// Each monitor has its own strip and its own nine workspaces, like niri. Shortcuts act on the
/// focused monitor (the focused window's, else the pointer's); new windows join the monitor they
/// open on. Nothing is left overlapping a neighbouring monitor: columns that would park or peek
/// across a shared edge wait hidden instead.
/// </remarks>
internal sealed class WindowManager : IDisposable
{
    private const nuint AnimationTimer = 1, SettleTimer = 2, WheelTimer = 4, SnapshotTimer = 5;
    /// <summary>How far one wheel notch moves the strip, as a share of the screen.</summary>
    private const double NotchFraction = 0.12;
    private static readonly string snapshotPath = Path.Combine(Settings.Directory, "layouts.json");
    private static readonly string memoryPath = Path.Combine(Settings.Directory, "memory.json");

    /// <summary>One monitor: where it is, its scale, and its workspaces.</summary>
    private sealed class Display(MonitorInfo monitor)
    {
        public MonitorInfo Monitor { get; set; } = monitor;
        public string Device => Monitor.Device;
        public Rect WorkArea => Monitor.WorkArea;
        public WorkspaceSet Spaces { get; } = new(monitor.WorkArea);

        public override string ToString() => Device;
    }

    private readonly MessageWindow window;
    private Settings settings;
    private AppRules rules;
    private WinEventHooks? events;
    private List<Display> displays = [];
    /// <summary>The monitor shortcuts act on.</summary>
    private Display? focused;

    /// <summary>Where each tiled window was last sent (visible frame, screen pixels).</summary>
    private readonly Dictionary<WindowId, Rect> placed = [];
    private readonly Dictionary<WindowId, Borders> borders = [];
    /// <summary>Windows that left (minimized, hidden, cloaked), their monitor, and where to put them back.</summary>
    private readonly Dictionary<WindowId, (string Device, WorkspaceSet.Departure Departure)> departures = [];
    private readonly HashSet<WindowId> userFloating = [], userTiled = [];
    /// <summary>Windows waiting out of sight: hidden tabs, inactive workspaces, columns kept off a neighbouring monitor.</summary>
    private readonly HashSet<WindowId> hidden = [];

    private Dictionary<WindowId, (Rect From, Rect To)> motions = [];
    private long animationStart;
    private bool timerResolutionRaised;

    /// <summary>Glide just focused this window; ignore other foreground changes until the deadline.</summary>
    private (WindowId Window, long Until)? focusLock;
    private long workspaceSwitchLockUntil;
    /// <summary>The window whose border Glide colored, and the color.</summary>
    private (WindowId Window, uint Color)? ringed;
    private readonly FocusRingWindow ring = new();
    /// <summary>The window the user is dragging or resizing with the mouse; the ring waits until they let go.</summary>
    private WindowId? dragging;
    private IReadOnlyList<TabBar> currentTabBars = [];
    /// <summary>Where apps go, learned from the layout: see <see cref="AppMemory"/>.</summary>
    private AppMemory memory = LoadMemory();
    /// <summary>Each tiled window's app, looked up once.</summary>
    private readonly Dictionary<WindowId, string?> appIds = [];

    public WindowManager(MessageWindow window, Settings settings)
    {
        this.window = window;
        this.settings = settings;
        rules = new AppRules(settings.Rules);
    }

    public bool IsEnabled { get; private set; }

    /// <summary>The focused monitor's set; most actions work on it.</summary>
    private WorkspaceSet spaces => (focused ?? displays[0]).Spaces;

    public int ActiveWorkspaceNumber => displays.Count == 0 ? 1 : spaces.ActiveIndex + 1;

    /// <summary>Where tab bars go now (empty while tiling is off).</summary>
    public event Action<IReadOnlyList<TabBar>>? TabBarsChanged;

    /// <summary>The workspace changed or a column went to another one: for the on-screen display.</summary>
    public event Action<WorkspaceAnnouncement>? WorkspaceAnnounced;

    /// <summary>The workspace or the enabled state changed.</summary>
    public event Action? StateChanged;

    private IEnumerable<WindowId> AllWindows => displays.SelectMany(d => d.Spaces.AllWindows);

    /// <summary>The monitor and workspace holding a tiled window.</summary>
    private (Display Display, int Workspace)? Find(WindowId id)
    {
        foreach (var display in displays)
            if (display.Spaces.IndexOf(id) is int workspace) return (display, workspace);
        return null;
    }

    private Display DisplayNamed(string? device) => displays.FirstOrDefault(d => d.Device == device) ?? displays[0];

    private Display DisplayOf(HWND hwnd) => DisplayNamed(Native.MonitorOf(hwnd));

    public void ApplySettings(Settings newSettings)
    {
        var rulesChanged = !newSettings.Rules.SequenceEqual(settings.Rules);
        settings = newSettings;
        rules = new AppRules(settings.Rules);
        if (!IsEnabled) return;
        UpdateDisplays();
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
        foreach (var id in AllWindows.ToList())
        {
            var hwnd = Native.Hwnd(id);
            if (!Native.Exists(hwnd) || ShouldTile(hwnd)) continue;
            Float(id, hwnd);
            Log.Info($"A rule floats {Describe(id)}");
        }
        foreach (var hwnd in Native.TopLevelWindows())
        {
            var id = Native.Id(hwnd);
            if (Find(id) is not null || departures.ContainsKey(id) || !ShouldTile(hwnd, out var rule)) continue;
            DisplayOf(hwnd).Spaces.Insert(id, rule?.WidthIndex ?? settings.DefaultWidthIndex(), rule?.Workspace, focus: false);
            Log.Info($"A rule tiles {Describe(id)}");
        }
    }

    /// <summary>Takes a window off the strip and puts it, smaller, in the middle of its screen.</summary>
    private void Float(WindowId id, HWND hwnd)
    {
        var display = Find(id)?.Display ?? DisplayOf(hwnd);
        display.Spaces.Remove(id);
        placed.Remove(id);
        hidden.Remove(id);
        motions.Remove(id);
        Native.SetVisibleFrame(hwnd, FloatingFrame(hwnd, display), Native.BordersOf(hwnd));
    }

    /// <summary>
    /// Colors the border of the focused window when it's tiled on its monitor's active workspace,
    /// and puts back the border of the one before.
    /// </summary>
    private void UpdateFocusRing() => UpdateFocusRing(landed: false);

    /// <param name="landed">Windows have reached their places, so their actual frames are right.</param>
    private void UpdateFocusRing(bool landed)
    {
        (WindowId Window, uint Color)? target = null;
        if (IsEnabled && settings.FocusRing)
        {
            var foreground = Native.Id(Native.Foreground);
            if (Find(foreground) is var (display, _) && display.Spaces.Active.Strip.Contains(foreground))
                target = (foreground, Native.ParseColor(settings.FocusRingColor));
        }
        RefreshTabBars();
        DrawRing(target, landed);
        if (target == ringed) return;
        // Windows' own border too, as the ring's inner edge, where the app has one.
        if (ringed is var (old, _) && Native.Exists(Native.Hwnd(old))) Native.SetBorderColor(Native.Hwnd(old), null);
        if (target is var (window, color)) Native.SetBorderColor(Native.Hwnd(window), color);
        ringed = target;
    }

    /// <summary>Shows the ring around the focused window once it's in place: not while it slides, waits hidden, or is being dragged.</summary>
    private void DrawRing((WindowId Window, uint Color)? target, bool landed)
    {
        // Windows move asynchronously, so right after a layout a window may not be where it was
        // sent yet: the ring goes where it's going, and is corrected once windows have landed.
        Rect? Frame(WindowId id) => !landed && placed.TryGetValue(id, out var sent) ? sent : Native.VisibleFrame(Native.Hwnd(id));
        if (target is not var (id, color) || settings.FocusRingWidth <= 0 || motions.ContainsKey(id) || hidden.Contains(id) || dragging == id
            || Find(id) is not var (display, _) || Frame(id) is not { } frame)
        {
            ring.Hide();
            return;
        }
        var scale = display.Monitor.Scale;
        // Windows 11 rounds window corners by 8 px at 100%.
        var radius = Environment.OSVersion.Version.Build >= 22000 ? (int)Math.Round(8 * scale) : 0;
        ring.Show(Native.Hwnd(id), frame, color, Math.Max(1, (int)Math.Round(settings.FocusRingWidth * scale)), radius);
    }

    // Enabling and disabling

    public void Enable()
    {
        if (IsEnabled) return;
        IsEnabled = true;
        displays = [];
        UpdateDisplays();
        events = new WinEventHooks(OnWinEvent);

        var snapshots = LoadSnapshots();
        foreach (var snapshot in snapshots.Values)
        {
            userFloating.UnionWith(snapshot.UserFloating.Where(w => Native.Exists(Native.Hwnd(w))));
            userTiled.UnionWith(snapshot.UserTiled.Where(w => Native.Exists(Native.Hwnd(w))));
        }

        // Windows join left to right in their current on-screen order, on the monitor they're on.
        // Windows a saved layout knows go back to that layout's monitor (hidden ones may sit elsewhere).
        var adopted = Native.TopLevelWindows()
            .Where(ShouldTile)
            .Select(hwnd => (Id: Native.Id(hwnd), Hwnd: hwnd, Frame: Native.VisibleFrame(hwnd) ?? default))
            .OrderBy(w => w.Frame.X).ThenBy(w => w.Frame.Y)
            .ToList();
        // Windows from this session's saved layout go back exactly where they were.
        foreach (var display in displays)
        {
            if (!snapshots.TryGetValue(display.Device, out var snapshot)) continue;
            var saved = snapshot.AllWindows.ToHashSet();
            var mine = adopted.Where(w => saved.Contains(w.Id)).Select(w => w.Id).ToList();
            var (strips, active) = snapshot.RestoreWorkspaces(mine, settings.DefaultWidthIndex(), display.Spaces.Settings.WidthPresets.Count);
            display.Spaces.Restore(strips, active);
        }
        // The rest (after a restart, or new since) go where their apps usually are, in the
        // remembered order; apps Glide doesn't know join the monitor they're on, left to right.
        var others = adopted.Where(w => Find(w.Id) is null)
            .Select(w => (w.Id, Place: Remembered(w.Hwnd, rule: null)))
            .GroupBy(w => (w.Place.Display, w.Place.Workspace));
        foreach (var group in others)
        {
            var (target, index) = group.Key;
            var strip = target.Spaces.Workspaces[index].Strip;
            foreach (var (id, place) in memory.Arrange(target.Device, index, group.ToList(), w => AppIdOf(w.Id)))
                strip.Insert(place.Column(id), at: strip.Columns.Count, focus: false);
        }

        var foreground = Native.Id(Native.Foreground);
        focused = Find(foreground)?.Display ?? DisplayNamed(Native.MonitorAtCursor());
        if (Find(foreground) is var (home, workspace))
        {
            home.Spaces.Activate(workspace);
            home.Spaces.Active.Strip.Focus(foreground);
        }
        foreach (var display in displays) display.Spaces.Active.EnsureFocusedVisible();
        Log.Info($"Tiling on: {AllWindows.Count()} windows on {displays.Count} monitor(s); " +
                 string.Join("; ", displays.Select(d => $"{d.Device} workspace {d.Spaces.ActiveIndex + 1}: {d.Spaces.Active.Strip}")));
        Layout();
        UpdateFocusRing();
        StateChanged?.Invoke();
    }

    /// <summary>Stops tiling and moves every window somewhere fully on screen, hidden workspaces included.</summary>
    public void Disable()
    {
        if (!IsEnabled) return;
        FinishAnimations();
        SaveSnapshots();
        LearnPlacements();
        IsEnabled = false;
        UpdateFocusRing();
        var restoredCount = 0;
        foreach (var display in displays)
        {
            var frames = display.Spaces.AllWindows
                .Where(id => Native.Exists(Native.Hwnd(id)))
                .Select(id => (id, placed.TryGetValue(id, out var frame) ? frame : Native.VisibleFrame(Native.Hwnd(id)) ?? default))
                .ToList();
            var restored = RestorePlanner.RestoredFrames(frames, display.WorkArea, inset: display.Spaces.Settings.Gap);
            foreach (var (id, frame) in restored)
                Native.SetVisibleFrame(Native.Hwnd(id), frame, borders.GetValueOrDefault(id));
            restoredCount += restored.Count;
        }
        Log.Info($"Tiling off: restored {restoredCount} windows");

        events?.Dispose();
        events = null;
        window.StopTimer(SettleTimer);
        displays = [];
        focused = null;
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
                return;
            case GlideAction.FocusMonitorLeft or GlideAction.FocusMonitorRight:
                FocusMonitor(action == GlideAction.FocusMonitorLeft ? -1 : 1);
                return;
            case GlideAction.MoveColumnToMonitorLeft or GlideAction.MoveColumnToMonitorRight:
                MoveColumnToMonitor(action == GlideAction.MoveColumnToMonitorLeft ? -1 : 1);
                return;
        }
        if (!spaces.Active.Perform(action)) return;
        Layout();
        FocusActive();
    }

    /// <summary>
    /// The user may have clicked a window, or moved the pointer to another monitor, since the last
    /// event; act where they're looking.
    /// </summary>
    private void SyncFocusFromForeground()
    {
        var foreground = Native.Id(Native.Foreground);
        if (Find(foreground) is var (display, _) && display.Spaces.Active.Strip.Contains(foreground))
        {
            focused = display;
            display.Spaces.Active.Strip.Focus(foreground);
        }
        else if (Native.MonitorAtCursor() is { } device)
        {
            // Nothing tiled has focus (the desktop, a floating window): go by the pointer.
            focused = DisplayNamed(device);
        }
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
        Log.Info($"Workspace {index + 1} on {focused}: {spaces.Active.Strip}");
        StateChanged?.Invoke();
        Announce(spaces.Active.Strip.IsEmpty ? $"Workspace {index + 1} is empty" : $"Workspace {index + 1}");
    }

    private void Announce(string message) =>
        WorkspaceAnnounced?.Invoke(new WorkspaceAnnouncement(
            [.. spaces.Occupied.Append(spaces.ActiveIndex).Distinct().Order().Select(i => (i + 1, i == spaces.ActiveIndex))], message,
            (focused ?? displays[0]).WorkArea));

    /// <summary>Monitors left to right, for the monitor shortcuts.</summary>
    private Display? Neighbour(int direction)
    {
        if (focused is null) return null;
        return MonitorGeometry.Neighbour([.. displays.Select(d => d.Monitor.Bounds)], displays.IndexOf(focused), direction) is int index
            ? displays[index] : null;
    }

    private void FocusMonitor(int direction)
    {
        if (Neighbour(direction) is not { } target) return;
        focused = target;
        FocusActive();
        StateChanged?.Invoke();
        Announce($"Workspace {spaces.ActiveIndex + 1}");
    }

    /// <summary>Sends the focused column to the next monitor that way, where it lands right of the focused column and keeps focus.</summary>
    private void MoveColumnToMonitor(int direction)
    {
        if (focused is not { } source || Neighbour(direction) is not { } target) return;
        if (source.Spaces.Active.Strip.RemoveFocusedColumn() is not { } column) return;
        source.Spaces.Active.SettleScroll();
        target.Spaces.Active.Strip.Insert(column);
        target.Spaces.Active.EnsureFocusedVisible();
        foreach (var id in column.Windows) placed.Remove(id);
        focused = target;
        Log.Info($"Moved column to {target}: {target.Spaces.Active.Strip}");
        Layout();
        FocusActive();
        StateChanged?.Invoke();
    }

    // Scrolling with the mouse wheel

    /// <summary>
    /// Moves the strip under the pointer with the wheel (in notches; positive goes right),
    /// rubber-banding past the ends. Shortly after the wheel stops, it snaps to the nearest column
    /// edge and keeps focus on a column that's fully on screen, like ending a trackpad swipe.
    /// </summary>
    public void ScrollStrip(double notches)
    {
        if (!IsEnabled) return;
        var display = DisplayNamed(Native.MonitorAtCursor());
        var workspace = display.Spaces.Active;
        if (workspace.Strip.IsEmpty) return;
        FinishAnimations();
        workspace.Scroll(notches * workspace.UsableWidth * NotchFraction);
        Layout(animate: false);
        window.StopTimer(WheelTimer);
        window.StartTimer(WheelTimer, 220, () =>
        {
            window.StopTimer(WheelTimer);
            if (!IsEnabled || !displays.Contains(display)) return;
            var focusMoved = display.Spaces.Active.SettleSwipe(display.Spaces.Active.ScrollOffset);
            Log.Debug($"Wheel scroll on {display} settled at {display.Spaces.Active.ScrollOffset}; focus {display.Spaces.Active.Strip.FocusedWindow}{(focusMoved ? " (moved)" : "")}");
            Layout();
            if (focusMoved)
            {
                focused = display;
                FocusActive();
            }
        });
    }

    // Trackpad swipes

    /// <summary>How far the strip moves per touchpad width the fingers travel, in screen widths (as on macOS).</summary>
    private const double SwipeGain = 1.5;
    /// <summary>How far momentum carries the strip after the fingers lift: seconds of travel at release speed.</summary>
    private const double SwipeMomentum = 0.35;
    private Display? swiping;

    public void BeginSwipe()
    {
        if (!IsEnabled) return;
        var display = DisplayNamed(Native.MonitorAtCursor());
        if (display.Spaces.Active.Strip.IsEmpty) return;
        FinishAnimations();
        window.StopTimer(WheelTimer);
        swiping = display;
    }

    /// <summary>Moves the strip with the fingers; <paramref name="delta"/> in touchpad widths, positive to the right.</summary>
    public void Swipe(double delta)
    {
        if (!IsEnabled || swiping is not { } display || !displays.Contains(display)) return;
        // Fingers going left pull the strip left, revealing columns on the right.
        display.Spaces.Active.Scroll(-delta * display.WorkArea.Width * SwipeGain);
        Layout(animate: false);
    }

    /// <summary>Lets go: snaps near where momentum takes the strip and keeps focus on a column on screen.</summary>
    public void EndSwipe(double velocity)
    {
        if (swiping is not { } display) return;
        swiping = null;
        if (!IsEnabled || !displays.Contains(display)) return;
        var workspace = display.Spaces.Active;
        var projected = workspace.ScrollOffset - velocity * display.WorkArea.Width * SwipeGain * SwipeMomentum;
        var focusMoved = workspace.SettleSwipe(projected);
        Log.Debug($"Swipe ended at {velocity:0.00} widths/s on {display}; settled at {workspace.ScrollOffset}");
        Layout();
        if (focusMoved)
        {
            focused = display;
            FocusActive();
        }
    }

    // Overview and tabs

    /// <summary>Everything the overview shows for the focused monitor, taken when it opens; null while tiling is off.</summary>
    public OverviewData? MakeOverview()
    {
        if (!IsEnabled) return null;
        SyncFocusFromForeground();
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

    /// <summary>Shows and focuses a tiled window, switching monitor and workspace if needed (overview, tab bars).</summary>
    public void GoTo(WindowId id)
    {
        if (!IsEnabled || Find(id) is not var (display, workspace)) return;
        focused = display;
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
            var progress = AnimationProgress();
            foreach (var display in displays)
            {
                foreach (var bar in display.Spaces.Active.TabBars())
                {
                    // While its column slides, the bar rides along with the active tab.
                    var frame = bar.Frame;
                    if (motions.TryGetValue(bar.ActiveWindow, out var motion))
                    {
                        var now = FrameAnimation.Frame(motion.From, motion.To, progress);
                        frame = frame.Offset(now.X - motion.To.X, now.Y - motion.To.Y);
                    }
                    bars.Add(new TabBar(frame, [.. bar.Windows.Select(w => (w, Native.Title(Native.Hwnd(w))))], bar.ActiveIndex,
                                        bar.Windows.Contains(foreground)));
                }
            }
        }
        currentTabBars = bars;
        var key = string.Join("|", bars.Select(b => $"{b.Frame}{b.ActiveIndex}{b.IsFocused}{string.Join(",", b.Tabs)}"));
        if (key == lastTabBars) return;
        lastTabBars = key;
        TabBarsChanged?.Invoke(bars);
    }

    private void ToggleFloating()
    {
        var hwnd = Native.Foreground;
        var id = Native.Id(hwnd);
        if (Find(id) is not null)
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
            focused = DisplayOf(hwnd);
            spaces.Insert(id, settings.DefaultWidthIndex(), focus: true);
            Layout();
        }
    }

    private static Rect FloatingFrame(HWND hwnd, Display display)
    {
        var viewport = display.WorkArea;
        var current = Native.VisibleFrame(hwnd) ?? new Rect(0, 0, viewport.Width / 2, viewport.Height / 2);
        var width = Math.Min(current.Width, viewport.Width * 0.7);
        var height = Math.Min(current.Height, viewport.Height * 0.7);
        return new Rect(Math.Round(viewport.MidX - width / 2), Math.Round(viewport.MidY - height / 2), width, height);
    }

    private void FocusActive()
    {
        if (spaces.Active.Strip.FocusedWindow is { } window)
        {
            focusLock = (window, Deadline(0.4));
            Native.Focus(Native.Hwnd(window));
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
            case Win32.EVENT_SYSTEM_MOVESIZESTART:
                dragging = id;
                break;
            case Win32.EVENT_SYSTEM_MOVESIZEEND:
                dragging = null;
                OnUserMoveOrResize(id, hwnd);
                break;
        }
        UpdateFocusRing();
    }

    private void OnForeground(WindowId id, HWND hwnd)
    {
        if (focusLock is var (locked, until) && Stopwatch.GetTimestamp() < until && locked != id) return;
        if (Find(id) is not var (display, workspace))
        {
            // Some windows only become tileable after they first show (a title arrives late).
            Appear(id, hwnd, focus: true);
            return;
        }
        focused = display;
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
        StateChanged?.Invoke();
    }

    private void Appear(WindowId id, HWND hwnd, bool focus)
    {
        if (Find(id) is not null) return;
        if (departures.TryGetValue(id, out var left))
        {
            if (!Native.Inspect(hwnd).IsCandidate) return;
            departures.Remove(id);
            var home = DisplayNamed(left.Device);
            var workspace = home.Spaces.Rejoin(id, left.Departure, focus);
            Log.Debug($"Returned {Describe(id)} to workspace {workspace + 1} on {home}");
            Layout();
            return;
        }
        if (!ShouldTile(hwnd, out var rule)) return;
        // New windows go where their app usually is (or the rule says); otherwise they join the
        // monitor the app opened them on, right of the focused column.
        var place = Remembered(hwnd, rule);
        var (display, target) = (place.Display, place.Workspace);
        var strip = display.Spaces.Workspaces[target].Strip;
        var index = settings.RememberApps
            ? memory.InsertionIndex(display.Device, target, AppIdOf(id), [.. strip.Columns.Select(c => AppIdOf(c.Windows[0]))])
            : null;
        strip.Insert(place.Column(id), index, focus);
        if (target == display.Spaces.ActiveIndex || focus) display.Spaces.Workspaces[target].EnsureFocusedVisible();
        Log.Info($"Tiling {Describe(id)} on workspace {target + 1} of {display}: {strip}");
        if (focus)
        {
            focused = display;
            // Just opened, and it belongs on another workspace: go there with it rather than hide it.
            if (target != display.Spaces.ActiveIndex && Native.Foreground == hwnd)
            {
                SwitchWorkspace(target, focusing: id);
                return;
            }
        }
        Layout();
    }

    private void Depart(WindowId id)
    {
        if (Find(id) is not var (display, _) || display.Spaces.DepartureOf(id) is not { } departure) return;
        var wasFocused = focused == display && spaces.Active.Strip.FocusedWindow == id;
        departures[id] = (display.Device, departure);
        display.Spaces.Remove(id);
        placed.Remove(id);
        hidden.Remove(id);
        Log.Debug($"{Describe(id)} left: {display.Spaces.Active.Strip}");
        Layout();
        // Windows would otherwise activate whatever is next in z-order, maybe on another workspace.
        if (wasFocused) FocusActive();
    }

    private void Forget(WindowId id)
    {
        appIds.Remove(id);
        departures.Remove(id);
        userFloating.Remove(id);
        userTiled.Remove(id);
        if (Find(id) is not var (display, _)) return;
        var wasFocused = focused == display && spaces.Active.Strip.FocusedWindow == id;
        display.Spaces.Remove(id);
        placed.Remove(id);
        borders.Remove(id);
        hidden.Remove(id);
        motions.Remove(id);
        Log.Debug($"Window {id} closed: {display.Spaces.Active.Strip}");
        Layout();
        if (wasFocused) FocusActive();
    }

    /// <summary>
    /// After the user moves or resizes a window with the mouse. A resized window keeps its new
    /// width: its column takes it and the strip reflows. A window dropped on a tab bar joins those
    /// tabs, and one dropped on another tiled window joins that window's column (a stack, or
    /// tabs). A window dropped on another monitor moves there. Otherwise it snaps back. Floating
    /// windows can be dropped onto tab bars and columns too, which tiles them there.
    /// </summary>
    private void OnUserMoveOrResize(WindowId id, HWND hwnd)
    {
        if (Native.VisibleFrame(hwnd) is not { } actual) return;
        var home = Find(id);
        if (home is var (display, workspace) && placed.TryGetValue(id, out var applied) && Math.Abs(actual.Width - applied.Width) > 2)
        {
            var strip = display.Spaces.Workspaces[workspace].Strip;
            if (strip.Location(id) is (int column, _))
            {
                strip.SetCustomWidth(actual.Width, id);
                foreach (var sibling in strip.Columns[column].Windows) placed.Remove(sibling);
                display.Spaces.Workspaces[workspace].EnsureFocusedVisible();
                Log.Debug($"Adopted width {actual.Width} for {Describe(id)}");
            }
            placed[id] = actual;
            Layout();
            return;
        }
        if (Native.CursorPosition is { } cursor && DropTarget(id, cursor) is { } drop)
        {
            if (home is null && !Native.Inspect(hwnd).CanTile) return;
            MoveIntoColumn(id, drop.Display, drop.Sibling);
            return;
        }
        if (home is var (from, _) && Native.CursorPosition is { } point && DisplayNamed(Native.MonitorAtCursor()) is var to && to != from
            && to.WorkArea.Contains(new Rect(point.X, point.Y, 0, 0)))
        {
            MoveToDisplay(id, from, to, point.X);
            return;
        }
        if (home is not null)
        {
            placed[id] = actual;
            Layout();
        }
    }

    /// <summary>The tiled window whose column a drop at <paramref name="point"/> joins: a tab bar's, or the window under it.</summary>
    private (Display Display, WindowId Sibling)? DropTarget(WindowId dragged, Point point)
    {
        bool Inside(Rect frame) => point.X >= frame.MinX && point.X < frame.MaxX && point.Y >= frame.MinY && point.Y < frame.MaxY;
        foreach (var bar in currentTabBars)
        {
            if (!Inside(bar.Frame) || bar.Tabs.Any(t => t.Window == dragged)) continue;
            if (Find(bar.ActiveWindow) is var (display, _)) return (display, bar.ActiveWindow);
        }
        foreach (var display in displays)
        {
            foreach (var placement in display.Spaces.Active.Placements())
            {
                if (placement.Window == dragged || placement.Visibility is not (Visibility.Visible or Visibility.Partial) || !Inside(placement.Frame)) continue;
                // Not onto its own column: that's just a move, and it snaps back.
                if (display.Spaces.Active.Strip.Location(dragged) is (int own, _) && display.Spaces.Active.Strip.Location(placement.Window) is (int other, _) && own == other)
                    return null;
                return (display, placement.Window);
            }
        }
        return null;
    }

    /// <summary>Takes a window from wherever it is and adds it to the bottom (or the tabs) of <paramref name="sibling"/>'s column.</summary>
    private void MoveIntoColumn(WindowId id, Display display, WindowId sibling)
    {
        Find(id)?.Display.Spaces.Remove(id);
        userFloating.Remove(id);
        var workspace = display.Spaces.Active;
        workspace.Strip.Join(id, sibling, focus: true);
        workspace.EnsureFocusedVisible();
        placed.Remove(id);
        focused = display;
        Log.Info($"Dropped {Describe(id)} into a column on {display}: {workspace.Strip}");
        Layout();
        FocusActive();
    }

    /// <summary>Moves a window dragged to another monitor into that monitor's strip, as its own column near where it was dropped.</summary>
    private void MoveToDisplay(WindowId id, Display from, Display to, double x)
    {
        var departure = from.Spaces.DepartureOf(id);
        from.Spaces.Remove(id);
        var workspace = to.Spaces.Active;
        var index = MonitorGeometry.DropIndex(workspace, x);
        workspace.Strip.Insert(new Column([id], widthIndex: departure?.WidthIndex ?? settings.DefaultWidthIndex(),
                                          isFullWidth: departure?.IsFullWidth ?? false), index, focus: true);
        workspace.EnsureFocusedVisible();
        placed.Remove(id);
        focused = to;
        Log.Info($"Moved {Describe(id)} to {to}: {workspace.Strip}");
        Layout();
        FocusActive();
        StateChanged?.Invoke();
    }

    /// <summary>Where a window goes and how wide: its app's rule first, then where the app was last, then the defaults.</summary>
    private sealed record Placement(Display Display, int Workspace, int WidthIndex, bool IsFullWidth, double? CustomWidth)
    {
        public Column Column(WindowId id) => new([id], widthIndex: WidthIndex, isFullWidth: IsFullWidth, customWidth: CustomWidth);
    }

    private Placement Remembered(HWND hwnd, AppRule? rule)
    {
        var id = Native.Id(hwnd);
        var app = settings.RememberApps ? memory.Lookup(AppIdOf(id)) : null;
        var display = app?.Monitor is { } monitor && displays.Any(d => d.Device == monitor) ? DisplayNamed(monitor) : DisplayOf(hwnd);
        var workspace = rule?.Workspace ?? app?.Workspace ?? display.Spaces.ActiveIndex;
        if (workspace is < 0 or >= WorkspaceSet.Count) workspace = display.Spaces.ActiveIndex;
        if (rule?.WidthIndex is { } ruleWidth) return new Placement(display, workspace, ruleWidth, false, null);
        if (app is null) return new Placement(display, workspace, settings.DefaultWidthIndex(), false, null);
        var presets = display.Spaces.Settings.WidthPresets.Count;
        return new Placement(display, workspace, app.WidthIndex >= 0 && app.WidthIndex < presets ? app.WidthIndex : settings.DefaultWidthIndex(),
                             app.IsFullWidth, app.WidthFraction is { } fraction ? Math.Round(fraction * display.WorkArea.Width) : null);
    }

    private string? AppIdOf(WindowId id)
    {
        if (appIds.TryGetValue(id, out var app)) return app;
        app = Native.Inspect(Native.Hwnd(id)).AppId;
        appIds[id] = app;
        return app;
    }

    /// <summary>Updates what Glide remembers about apps from the current layout, and saves it.</summary>
    private void LearnPlacements()
    {
        if (!settings.RememberApps) return;
        var strips = new List<AppMemory.Strip>();
        foreach (var display in displays)
        {
            for (var index = 0; index < display.Spaces.Workspaces.Count; index++)
            {
                var columns = display.Spaces.Workspaces[index].Strip.Columns;
                if (columns.Count == 0) continue;
                strips.Add(new AppMemory.Strip(display.Device, index, display.WorkArea.Width,
                    [.. columns.Select(c => (c, (IReadOnlyList<string?>)[.. c.Windows.Select(AppIdOf)]))]));
            }
        }
        memory.Learn(strips);
        try
        {
            Directory.CreateDirectory(Settings.Directory);
            File.WriteAllText(memoryPath, memory.ToJson());
        }
        catch (Exception e)
        {
            Log.Error("Couldn't save where apps go", e);
        }
    }

    private static AppMemory LoadMemory()
    {
        try
        {
            return File.Exists(memoryPath) ? AppMemory.FromJson(File.ReadAllText(memoryPath)) : new AppMemory();
        }
        catch (Exception e)
        {
            Log.Error("Couldn't read where apps go", e);
            return new AppMemory();
        }
    }

    /// <summary>Forgets every app's remembered place (Settings › Forget).</summary>
    public void ForgetPlacements()
    {
        memory = new AppMemory();
        try
        {
            File.Delete(memoryPath);
        }
        catch (IOException)
        {
        }
        Log.Info("Forgot where apps go");
    }

    private bool ShouldTile(HWND hwnd) => ShouldTile(hwnd, out _);

    private bool ShouldTile(HWND hwnd, out AppRule? rule)
    {
        rule = null;
        var facts = Native.Inspect(hwnd);
        if (!facts.IsCandidate) return false;
        var id = Native.Id(hwnd);
        appIds[id] = facts.AppId;
        rule = rules.Rule(facts.AppId);
        return AppRules.ShouldTile(rule, facts.ClassifiedTileable, facts.CanTile, userFloating.Contains(id), userTiled.Contains(id));
    }

    // Monitors

    /// <summary>
    /// Matches monitors to displays by device name: new monitors get empty workspaces, and the
    /// windows of a monitor that's gone move to the primary one, workspace by workspace.
    /// </summary>
    public void UpdateDisplays()
    {
        var monitors = Native.Monitors();
        if (monitors.Count == 0) return;
        var previous = displays;
        displays = [.. monitors.Select(monitor =>
        {
            var display = previous.FirstOrDefault(d => d.Device == monitor.Device) ?? new Display(monitor);
            display.Monitor = monitor;
            var standard = LayoutSettings.Standard;
            var scale = monitor.Scale;
            display.Spaces.Settings = new LayoutSettings(
                gap: Math.Round(settings.Gap * scale), sliver: Math.Round(standard.Sliver * scale), widthPresets: standard.WidthPresets,
                defaultWidthIndex: standard.DefaultWidthIndex, tabBarHeight: Math.Round(standard.TabBarHeight * scale),
                tabBarSpacing: Math.Round(standard.TabBarSpacing * scale));
            display.Spaces.Viewport = monitor.WorkArea;
            return display;
        })];
        foreach (var gone in previous.Where(d => !displays.Contains(d)))
        {
            var primary = displays[0];
            for (var index = 0; index < WorkspaceSet.Count; index++)
            {
                foreach (var column in gone.Spaces.Workspaces[index].Strip.Columns.ToList())
                    primary.Spaces.Workspaces[index].Strip.Insert(column.Clone(), at: primary.Spaces.Workspaces[index].Strip.Columns.Count, focus: false);
            }
            foreach (var (id, left) in departures.Where(p => p.Value.Device == gone.Device).ToList())
                departures[id] = (primary.Device, left.Departure);
            Log.Info($"Monitor {gone} went away; its windows moved to {primary}");
        }
        if (focused is not null && !displays.Contains(focused)) focused = displays[0];
        foreach (var display in displays)
            foreach (var workspace in display.Spaces.Workspaces) workspace.SettleScroll();
    }

    public void OnDisplayChanged()
    {
        if (!IsEnabled) return;
        UpdateDisplays();
        placed.Clear();
        Layout(animate: false);
    }

    /// <summary>
    /// A pixel's worth of window peeking out at the bottom-right corner of the whole desktop: where
    /// hidden windows wait. Usually it's under the taskbar.
    /// </summary>
    private Point HiddenSpot() => MonitorGeometry.HiddenSpot([.. displays.Select(d => d.Monitor.Bounds)]);

    /// <summary>Whether a frame would show on a monitor other than its own.</summary>
    private bool CrossesIntoAnotherMonitor(Rect frame, Display own) =>
        MonitorGeometry.ShowsOnAnother(frame, displays.IndexOf(own), [.. displays.Select(d => d.Monitor.Bounds)]);

    // Layout

    /// <summary>Sends every tiled window to its slot; hidden ones wait out of sight.</summary>
    private void Layout(bool animate = true, int pass = 0)
    {
        if (!IsEnabled || displays.Count == 0) return;
        animate &= settings.Animate;
        var progress = AnimationProgress();
        var spot = HiddenSpot();
        var next = new Dictionary<WindowId, (Rect From, Rect To)>();

        foreach (var display in displays)
        {
            var viewport = display.WorkArea;
            for (var index = 0; index < display.Spaces.Workspaces.Count; index++)
            {
                var workspace = display.Spaces.Workspaces[index];
                var active = index == display.Spaces.ActiveIndex;
                foreach (var placement in workspace.Placements())
                {
                    var id = placement.Window;
                    var hwnd = Native.Hwnd(id);
                    var isHidden = !active || placement.Visibility == Visibility.HiddenTab
                        || (displays.Count > 1 && placement.Visibility is not Visibility.Visible && CrossesIntoAnotherMonitor(placement.Frame, display));
                    var to = isHidden ? new Rect(spot, placement.Frame.Size) : placement.Frame;
                    var wasHidden = isHidden ? !hidden.Add(id) : hidden.Remove(id);
                    if (placed.TryGetValue(id, out var last) && last == to && !motions.ContainsKey(id)) continue;

                    var from = motions.TryGetValue(id, out var motion) ? FrameAnimation.Frame(motion.From, motion.To, progress)
                        : placed.TryGetValue(id, out last) ? last : Native.VisibleFrame(hwnd);
                    Native.Unmaximize(hwnd);
                    var windowBorders = borders[id] = Native.BordersOf(hwnd);
                    placed[id] = to;
                    // Hidden windows jump; tabs switch in place rather than sliding in from the corner.
                    if (animate && !isHidden && !wasHidden && from is { } start && FrameAnimation.ShouldAnimate(start, to, viewport))
                        next[id] = (start, to);
                    else
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
        }
        UpdateFocusRing();
        // Keep the saved layout fresh, so a relaunch after a crash re-tiles in the same order.
        window.StopTimer(SnapshotTimer);
        window.StartTimer(SnapshotTimer, 3000, () =>
        {
            window.StopTimer(SnapshotTimer);
            if (!IsEnabled) return;
            SaveSnapshots();
            LearnPlacements();
        });
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
        var learned = false;
        foreach (var display in displays)
        {
            var workspace = display.Spaces.Active;
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
                    workspace.EnsureFocusedVisible();
                    learned = true;
                }
                else if (Math.Abs(actual.X - frame.X) > 2 || Math.Abs(actual.Y - frame.Y) > 2 || Math.Abs(actual.Width - frame.Width) > 2)
                {
                    Native.SetVisibleFrame(hwnd, frame, borders[placement.Window] = Native.BordersOf(hwnd));
                }
            }
        }
        if (learned && pass < 2)
        {
            Log.Debug("Learned minimum widths; laying out again");
            Layout(animate: true, pass + 1);
            return;
        }
        UpdateFocusRing(landed: true);
    }

    private double AnimationProgress() =>
        motions.Count == 0 ? 1 : Stopwatch.GetElapsedTime(animationStart).TotalSeconds / FrameAnimation.DefaultDuration;

    private void AnimationTick()
    {
        var progress = AnimationProgress();
        foreach (var (id, (from, to)) in motions)
            Native.SetVisibleFrame(Native.Hwnd(id), FrameAnimation.Frame(from, to, progress), borders.GetValueOrDefault(id));
        if (currentTabBars.Count > 0) RefreshTabBars();
        if (progress >= 1)
        {
            motions = [];
            StopAnimationTimer();
            UpdateFocusRing();
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

    /// <summary>Saves each monitor's layout, keyed by its device name.</summary>
    private void SaveSnapshots()
    {
        try
        {
            Directory.CreateDirectory(Settings.Directory);
            var layouts = displays.ToDictionary(d => d.Device, d => JsonSerializer.Deserialize<JsonElement>(
                new StripSnapshot(d.Spaces, userFloating, userTiled, BootTime).ToJson()));
            File.WriteAllText(snapshotPath, JsonSerializer.Serialize(layouts, StripSnapshot.JsonOptions));
        }
        catch (Exception e)
        {
            Log.Error("Couldn't save the layout", e);
        }
    }

    private static Dictionary<string, StripSnapshot> LoadSnapshots()
    {
        try
        {
            if (!File.Exists(snapshotPath)) return [];
            var layouts = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(File.ReadAllText(snapshotPath)) ?? [];
            return layouts
                .Select(p => (p.Key, Snapshot: StripSnapshot.FromJson(p.Value.GetRawText())))
                .Where(p => p.Snapshot is not null && Math.Abs((p.Snapshot.BootTime - BootTime).TotalSeconds) < 60)
                .ToDictionary(p => p.Key, p => p.Snapshot!);
        }
        catch (Exception e)
        {
            Log.Error("Couldn't read the saved layout", e);
            return [];
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
        ring.Dispose();
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

/// <summary>The workspaces in use on a monitor (and the active one), what just happened, and that monitor's work area.</summary>
public sealed record WorkspaceAnnouncement(IReadOnlyList<(int Number, bool IsActive)> Workspaces, string Message, Rect Screen);

/// <summary>What the overview shows: the layout, window titles, what typing filters on, and the screen.</summary>
public sealed record OverviewData(Overview Overview, IReadOnlyDictionary<WindowId, string> Titles,
                                  IReadOnlyDictionary<WindowId, string> SearchText, Rect Viewport, int ActiveWorkspace);
