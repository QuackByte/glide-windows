using Glide.Core;
using Glide.Platform;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Graphics;

namespace Glide;

/// <summary>
/// General options, the shortcut recorder and app rules. Every change applies and saves at once.
/// </summary>
public sealed partial class SettingsWindow : Window
{
    private static readonly (string Name, string Value)[] ringColors =
    [
        ("Windows accent color", "accent"), ("Blue", "#0078D4"), ("Teal", "#00B7C3"), ("Green", "#10893E"),
        ("Orange", "#F7630C"), ("Red", "#E81123"), ("Purple", "#8764B8"), ("White", "#FFFFFF"),
    ];
    private static readonly string[] widthNames = ["Default", "⅓ of the screen", "½ of the screen", "⅔ of the screen", "Full width"];

    private readonly GlideHost host;
    private Settings settings;
    private string currentPage = "General";
    /// <summary>The action whose shortcut is being recorded.</summary>
    private GlideAction? recording;
    private (InfoBarSeverity Severity, string Message)? notice;

    public SettingsWindow(GlideHost host)
    {
        this.host = host;
        settings = host.Settings;
        InitializeComponent();
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(TitleBar);
        AppWindow.SetIcon("Assets/Glide.ico");

        var scale = Content.XamlRoot?.RasterizationScale ?? GetScale();
        AppWindow.Resize(new SizeInt32((int)(960 * scale), (int)(720 * scale)));
        if (DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary) is { } display)
        {
            var area = display.WorkArea;
            AppWindow.Move(new PointInt32(area.X + (area.Width - AppWindow.Size.Width) / 2, area.Y + (area.Height - AppWindow.Size.Height) / 2));
        }

        host.StateChanged += OnHostStateChanged;
        host.SettingsChanged += OnSettingsFileChanged;
        Closed += (_, _) =>
        {
            host.StateChanged -= OnHostStateChanged;
            host.SettingsChanged -= OnSettingsFileChanged;
            if (recording is not null) host.CancelRecording();
        };
        Navigation.SelectedItem = Navigation.MenuItems[0];
    }

    private double GetScale() => WinRT.Interop.WindowNative.GetWindowHandle(this) is var hwnd && hwnd != 0 ? GetDpiForWindow(hwnd) / 96.0 : 1;

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(nint hwnd);

    /// <summary>Shows a page from outside, e.g. "Shortcuts".</summary>
    public void ShowPage(string tag)
    {
        Navigation.SelectedItem = Navigation.MenuItems.OfType<NavigationViewItem>().FirstOrDefault(i => (string)i.Tag == tag) ?? Navigation.MenuItems[0];
    }

    private void OnNavigationSelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is not NavigationViewItem { Tag: string tag }) return;
        if (recording is not null) StopRecording();
        currentPage = tag;
        notice = null;
        Render();
    }

    private void OnSettingsFileChanged()
    {
        settings = host.Settings;
        if (recording is null) Render();
    }

    private void OnHostStateChanged()
    {
        if (currentPage == "General") Render();
    }

    private void Apply()
    {
        host.ApplySettings(settings);
        settings = host.Settings;
    }

    private void Render()
    {
        Page.Children.Clear();
        switch (currentPage)
        {
            case "Shortcuts": RenderShortcuts(); break;
            case "Rules": RenderRules(); break;
            default: RenderGeneral(); break;
        }
    }

    // General

    private void RenderGeneral()
    {
        Header("General");

        Section("Tiling");
        var tiling = Toggle(host.TilingEnabled, on => host.TilingEnabled = on);
        Card("Tile windows", $"Arrange windows as columns on a scrolling strip. {(settings.Keymap.Combo(GlideAction.ToggleTiling) is { } toggle ? KeyLabels.Describe(toggle) : "A shortcut")} turns it on or off from anywhere.", tiling);
        Card("Start Glide when I sign in",
            StartupEntry.StartsElevated ? "Starts as administrator." : host.IsElevated ? "Starts as administrator, like now." : null,
            Toggle(StartupEntry.IsEnabled, on =>
            {
                StartupEntry.IsEnabled = on;
                Render();
            }));
        Card("Take over Windows shortcuts",
            "Win+arrows, Win+1…9, Win+Tab and a few others go to Glide instead of Snap, the taskbar and virtual desktops. " +
            "Turn this off to give them back to Windows; you can then give Glide's actions other shortcuts.",
            Toggle(settings.OverrideWindowsShortcuts, on => { settings.OverrideWindowsShortcuts = on; Apply(); }));

        Section("Mouse");
        var wheel = Choice(["Always scrolls the strip", "Scrolls the strip while Win is held", "Scrolls the app, as usual"], (int)settings.HorizontalWheel, 280);
        wheel.SelectionChanged += (_, _) =>
        {
            settings.HorizontalWheel = (WheelMode)Math.Max(wheel.SelectedIndex, 0);
            Apply();
        };
        Card("Horizontal wheel",
            "Tilting the wheel, or a thumb wheel like the MX Master's, glides along the strip; it snaps to a column when you stop. " +
            "Win + the vertical wheel always does, for mice without one.", wheel);

        Section("Appearance");
        var gap = new Slider { Minimum = 0, Maximum = 48, StepFrequency = 2, Value = settings.Gap, Width = 220 };
        gap.ValueChanged += (_, e) =>
        {
            settings.Gap = e.NewValue;
            Apply();
        };
        Card("Gap between windows", "Also used around the edge of the screen.", gap);
        Card("Slide windows into place", "Columns glide instead of jumping.", Toggle(settings.Animate, on => { settings.Animate = on; Apply(); }));

        var colors = new ComboBox { Width = 220, ItemsSource = ringColors.Select(c => c.Name).ToList(), IsEnabled = settings.FocusRing };
        colors.SelectedIndex = Math.Max(Array.FindIndex(ringColors, c => c.Value.Equals(settings.FocusRingColor, StringComparison.OrdinalIgnoreCase)), 0);
        colors.SelectionChanged += (_, _) =>
        {
            settings.FocusRingColor = ringColors[Math.Max(colors.SelectedIndex, 0)].Value;
            Apply();
        };
        var ring = Toggle(settings.FocusRing, on =>
        {
            settings.FocusRing = on;
            colors.IsEnabled = on;
            Apply();
        });
        Card("Focus ring", "Colors the border of the focused window. Needs Windows 11.", ring);
        Card("Focus ring color", null, colors);

        Section("Administrator");
        if (host.IsElevated)
        {
            Card("Glide is running as administrator", "It can tile windows of apps that run as administrator.", null);
        }
        else
        {
            var restart = new Button { Content = "Restart as administrator" };
            restart.Click += (_, _) => host.RestartAsAdministrator();
            Card("Apps running as administrator",
                "Windows doesn't let Glide move their windows, and Glide's shortcuts don't work while one is focused. " +
                "Restart Glide as administrator to manage them too.", restart);
        }

        Section("Files");
        var openSettings = new Button { Content = "Open settings file" };
        openSettings.Click += (_, _) => GlideHost.OpenInEditor(Settings.FilePath);
        var openLog = new Button { Content = "Open log" };
        openLog.Click += (_, _) => GlideHost.OpenInEditor(Log.FilePath);
        Card("Settings and log", $"{Settings.FilePath}\n{Log.FilePath}", Row(openSettings, openLog));
    }

    // Shortcuts

    private void RenderShortcuts()
    {
        Header("Shortcuts");
        var keymap = settings.Keymap;

        var reset = new Button { Content = "Reset all to defaults", IsEnabled = keymap.Overrides.Count > 0 };
        reset.Click += (_, _) =>
        {
            settings.Shortcuts.Clear();
            Apply();
            notice = (InfoBarSeverity.Success, "All shortcuts are back to their defaults.");
            Render();
        };
        Page.Children.Add(Text(
            "Click a shortcut and press the keys you want. Esc cancels; Backspace removes the shortcut. " +
            "Keys go by position, so Win+[ is the key right of P on any keyboard layout.", secondary: true));
        Page.Children.Add(Row(reset));
        if (notice is var (severity, message))
            Page.Children.Add(new InfoBar { Severity = severity, Message = message, IsOpen = true, Margin = new Thickness(0, 8, 0, 0) });

        foreach (var group in Enum.GetValues<ActionGroup>())
        {
            Section(group switch
            {
                ActionGroup.Focus => "Focus",
                ActionGroup.Move => "Move",
                ActionGroup.Columns => "Columns",
                ActionGroup.Workspaces => "Workspaces",
                _ => "General",
            });
            foreach (var action in GlideActions.All.Where(a => a.Group() == group))
                ShortcutRow(action, keymap);
        }
    }

    private void ShortcutRow(GlideAction action, Keymap keymap)
    {
        var combo = keymap.Combo(action);
        var isRecording = recording == action;
        var button = new Button
        {
            MinWidth = 180,
            Content = isRecording ? "Press keys…" : combo is { } shown ? KeyLabels.Describe(shown) : "Not set",
            Style = isRecording ? (Style)Application.Current.Resources["AccentButtonStyle"] : null,
        };
        button.Click += (_, _) =>
        {
            if (recording == action)
                StopRecording();
            else
                StartRecording(action);
        };

        var controls = new List<UIElement> { button };
        if (keymap.Overrides.ContainsKey(action))
        {
            var undo = IconButton("", "Reset to default");
            undo.Click += (_, _) =>
            {
                var updated = settings.Keymap;
                updated.Reset(action);
                settings.SetKeymap(updated);
                Apply();
                notice = null;
                Render();
            };
            controls.Add(undo);
        }

        string? note = action switch
        {
            GlideAction.ConsumeOrExpelLeft => "Stacks the window into the column on its left; a window already in a stack moves out to its own column on the left.",
            GlideAction.ConsumeOrExpelRight => "Stacks the window into the column on its right; a window already in a stack moves out to its own column on the right.",
            GlideAction.ToggleTabbed => "Shows a stacked column's windows as tabs, one at a time.",
            _ => null,
        };
        if (GlideHost.Unavailable.Contains(action))
            note = "Coming with the overview. Until then Windows keeps this shortcut.";
        else if (note is null && combo is { } bound && ShortcutPolicy.ReplacedWindowsFeature(bound) is { } feature)
            note = settings.OverrideWindowsShortcuts ? $"Replaces Windows' “{feature}”." : $"Given back to Windows (“{feature}”). Choose another shortcut to use it in Glide.";
        Card(action.Title(), note, Row([.. controls]), compact: true);
    }

    private void StartRecording(GlideAction action)
    {
        if (recording is not null) host.CancelRecording();
        recording = action;
        notice = null;
        Render();
        host.RecordShortcut(combo => OnRecorded(action, combo));
    }

    private void StopRecording()
    {
        host.CancelRecording();
        recording = null;
        Render();
    }

    private void OnRecorded(GlideAction action, KeyCombo combo)
    {
        if (recording != action) return;
        recording = null;
        var keymap = settings.Keymap;

        if (combo is { KeyCode: KeyCode.Escape, Modifiers: KeyModifiers.None })
        {
            Render();
            return;
        }
        if (combo is { KeyCode: KeyCode.Backspace or KeyCode.Delete, Modifiers: KeyModifiers.None })
        {
            keymap.Set(null, action);
            settings.SetKeymap(keymap);
            Apply();
            notice = (InfoBarSeverity.Informational, $"{action.Title()} has no shortcut now.");
            Render();
            return;
        }

        switch (ShortcutPolicy.Evaluate(combo))
        {
            case ShortcutPolicy.Verdict.Rejected(var reason):
                notice = (InfoBarSeverity.Error, $"{KeyLabels.Describe(combo)} can't be used. {reason}");
                Render();
                return;
            case ShortcutPolicy.Verdict.Warning(var warning):
                notice = (InfoBarSeverity.Warning, $"{action.Title()} is now {KeyLabels.Describe(combo)}. {warning}");
                break;
            default:
                notice = null;
                break;
        }
        var displaced = keymap.Set(combo, action);
        settings.SetKeymap(keymap);
        Apply();
        if (displaced.Count > 0)
        {
            var others = string.Join(", ", displaced.Select(a => a.Title()));
            notice = (InfoBarSeverity.Informational, $"{KeyLabels.Describe(combo)} was used by {others}, which now has no shortcut.");
        }
        else if (notice is null && !settings.OverrideWindowsShortcuts && ShortcutPolicy.ReplacedWindowsFeature(combo) is { } feature)
        {
            notice = (InfoBarSeverity.Warning, $"{KeyLabels.Describe(combo)} stays with Windows (“{feature}”) while “Take over Windows shortcuts” is off.");
        }
        Render();
    }

    // Rules

    private void RenderRules()
    {
        Header("App rules");
        Page.Children.Add(Text(
            "Choose how an app's windows are treated. Automatic tiles standard resizable windows and floats dialogs and fixed-size ones. " +
            "A window you float or tile by hand keeps your choice.", secondary: true));

        var apps = host.RunningApps();
        var picker = new ComboBox
        {
            IsEditable = true,
            Width = 320,
            PlaceholderText = "App, e.g. notepad.exe",
            ItemsSource = apps.Select(a => a.AppId).ToList(),
        };
        var add = new Button { Content = "Add rule", Style = (Style)Application.Current.Resources["AccentButtonStyle"] };
        add.Click += (_, _) =>
        {
            var appId = (picker.SelectedItem as string ?? picker.Text)?.Trim();
            if (string.IsNullOrEmpty(appId)) return;
            if (!appId.Contains('.')) appId += ".exe";
            if (settings.Rules.Any(r => r.AppId.Equals(appId, StringComparison.OrdinalIgnoreCase))) return;
            var name = apps.FirstOrDefault(a => a.AppId.Equals(appId, StringComparison.OrdinalIgnoreCase)).Title;
            settings.Rules.Add(new AppRule(appId, AppName(appId, name), AppRule.RuleMode.Float));
            Apply();
            Render();
        };
        Card("Add a rule", "Pick an app with a window open, or type its executable name.", Row(picker, add));

        if (settings.Rules.Count > 0) Section("Rules");
        foreach (var rule in settings.Rules.ToList())
        {
            var mode = Choice(["Automatic", "Always float", "Always tile"], (int)rule.Mode, 150);
            var width = Choice(widthNames, (rule.WidthIndex ?? -1) + 1, 160);
            var workspace = Choice(["Active workspace", .. Enumerable.Range(1, WorkspaceSet.Count).Select(n => $"Workspace {n}")], (rule.Workspace ?? -1) + 1, 170);
            void Update(AppRule updated)
            {
                var index = settings.Rules.FindIndex(r => r.AppId == rule.AppId);
                if (index >= 0) settings.Rules[index] = updated;
                Apply();
            }
            mode.SelectionChanged += (_, _) => Update(Current() with { Mode = (AppRule.RuleMode)Math.Max(mode.SelectedIndex, 0) });
            width.SelectionChanged += (_, _) => Update(Current() with { WidthIndex = width.SelectedIndex <= 0 ? null : width.SelectedIndex - 1 });
            workspace.SelectionChanged += (_, _) => Update(Current() with { Workspace = workspace.SelectedIndex <= 0 ? null : workspace.SelectedIndex - 1 });
            AppRule Current() => settings.Rules.First(r => r.AppId == rule.AppId);

            var remove = IconButton("", "Remove rule");
            remove.Click += (_, _) =>
            {
                settings.Rules.RemoveAll(r => r.AppId == rule.AppId);
                Apply();
                Render();
            };
            Card(rule.AppName, rule.AppId, Row(mode, width, workspace, remove), compact: true);
        }
    }

    private static string AppName(string appId, string? title)
    {
        var name = Path.GetFileNameWithoutExtension(appId);
        return name.Length > 0 ? char.ToUpperInvariant(name[0]) + name[1..] : appId;
    }

    // Building blocks

    private void Header(string text) =>
        Page.Children.Add(new TextBlock { Text = text, Style = (Style)Application.Current.Resources["TitleTextBlockStyle"], Margin = new Thickness(0, 8, 0, 16) });

    private void Section(string text) =>
        Page.Children.Add(new TextBlock { Text = text, Style = (Style)Application.Current.Resources["BodyStrongTextBlockStyle"], Margin = new Thickness(0, 20, 0, 6) });

    private static TextBlock Text(string text, bool secondary = false)
    {
        var block = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 8) };
        if (secondary) block.Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"];
        return block;
    }

    private void Card(string title, string? description, UIElement? control, bool compact = false)
    {
        var grid = new Grid { ColumnSpacing = 16 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var texts = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Spacing = 2 };
        texts.Children.Add(new TextBlock { Text = title, TextWrapping = TextWrapping.Wrap });
        if (description is not null)
        {
            texts.Children.Add(new TextBlock
            {
                Text = description,
                TextWrapping = TextWrapping.Wrap,
                Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
                Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
            });
        }
        grid.Children.Add(texts);
        if (control is FrameworkElement element)
        {
            element.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(element, 1);
            grid.Children.Add(element);
        }

        Page.Children.Add(new Border
        {
            Child = grid,
            Padding = compact ? new Thickness(16, 8, 16, 8) : new Thickness(16, 14, 16, 14),
            MinHeight = compact ? 52 : 68,
            CornerRadius = new CornerRadius(6),
            BorderThickness = new Thickness(1),
            Background = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
            BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
        });
    }

    private static ToggleSwitch Toggle(bool value, Action<bool> changed)
    {
        var toggle = new ToggleSwitch { IsOn = value, OnContent = "On", OffContent = "Off", MinWidth = 0 };
        toggle.Toggled += (_, _) => changed(toggle.IsOn);
        return toggle;
    }

    private static ComboBox Choice(IReadOnlyList<string> items, int selected, double width) =>
        new() { ItemsSource = items.ToList(), SelectedIndex = Math.Clamp(selected, 0, items.Count - 1), Width = width };

    private static StackPanel Row(params UIElement[] children)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        foreach (var child in children) row.Children.Add(child);
        return row;
    }

    private static Button IconButton(string glyph, string tooltip)
    {
        var button = new Button { Content = new FontIcon { Glyph = glyph, FontSize = 14 }, Padding = new Thickness(8) };
        ToolTipService.SetToolTip(button, tooltip);
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(button, tooltip);
        return button;
    }
}
