using Glide.Core;
using Glide.Platform;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Graphics;
using static Glide.Core.Strings;

namespace Glide;

/// <summary>
/// General options, the shortcut recorder and app rules. Every change applies and saves at once.
/// </summary>
public sealed partial class SettingsWindow : Window
{
    private static readonly (string Name, string Value)[] ringColors =
    [
        (T("Windows accent color"), "accent"), (T("Blue"), "#0078D4"), (T("Teal"), "#00B7C3"), (T("Green"), "#10893E"),
        (T("Orange"), "#F7630C"), (T("Red"), "#E81123"), (T("Purple"), "#8764B8"), (T("White"), "#FFFFFF"),
    ];
    private static readonly string[] widthNames = [T("Default"), T("⅓ of the screen"), T("½ of the screen"), T("⅔ of the screen"), T("Full width")];

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
        // The XAML's words, in the user's language.
        Title = TitleText.Text = T("Glide Settings");
        foreach (var item in Navigation.MenuItems.OfType<NavigationViewItem>()) item.Content = T((string)item.Content);
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
        Header(T("General"));

        Section(T("Tiling"));
        var tiling = Toggle(host.TilingEnabled, on => host.TilingEnabled = on);
        Card(T("Tile windows"), T("Arrange windows as columns on a scrolling strip. {0} turns it on or off from anywhere.", settings.Keymap.Combo(GlideAction.ToggleTiling) is { } toggle ? KeyLabels.Describe(toggle) : T("A shortcut")), tiling);
        var layout = Choice([T("Scrolling"), T("Tiled")], (int)settings.DefaultLayout, 220);
        layout.SelectionChanged += (_, _) =>
        {
            settings.DefaultLayout = (LayoutMode)Math.Max(layout.SelectedIndex, 0);
            Apply();
        };
        Card(T("Layout"),
            T("Scrolling keeps columns on a strip you scroll along; tiled fits every window on screen, splitting the space in a spiral. New and untouched workspaces use this. {0} switches the current workspace.",
              settings.Keymap.Combo(GlideAction.ToggleLayout) is { } switchLayout ? KeyLabels.Describe(switchLayout) : T("A shortcut")), layout);
        var tiledColumns = Choice([T("Automatic"), "2", "3"], settings.DefaultTiledColumns is int columnCount ? columnCount - 1 : 0, 220);
        tiledColumns.SelectionChanged += (_, _) =>
        {
            settings.TiledColumns = tiledColumns.SelectedIndex switch { 1 => "2", 2 => "3", _ => "auto" };
            Apply();
        };
        Card(T("Tiled columns"),
            T("How many columns a tiled workspace uses before splitting the last one. Automatic uses 3 on ultrawide monitors. {0} switches the current workspace.",
              settings.Keymap.Combo(GlideAction.CycleTiledColumns) is { } switchColumns ? KeyLabels.Describe(switchColumns) : T("A shortcut")), tiledColumns);
        Card(T("Start Glide when I sign in"),
            StartupEntry.StartsElevated ? T("Starts as administrator.") : host.IsElevated ? T("Starts as administrator, like now.") : null,
            Toggle(StartupEntry.IsEnabled, on =>
            {
                StartupEntry.IsEnabled = on;
                Render();
            }));
        var forget = new Button { Content = T("Forget") };
        forget.Click += (_, _) =>
        {
            host.ForgetAppPlacements();
            forget.Content = T("Forgotten");
            forget.IsEnabled = false;
        };
        Card(T("Remember where apps go"),
            T("Windows open on their app's usual monitor and workspace, at the width and place on the strip you last gave them. After a restart, open windows are arranged the same way. App rules still come first."),
            Row(forget, Toggle(settings.RememberApps, on => { settings.RememberApps = on; Apply(); })));
        Card(T("Take over Windows shortcuts"),
            T("Win+arrows, Win+1…9, Win+Tab and a few others go to Glide instead of Snap, the taskbar and virtual desktops. Turn this off to give them back to Windows; you can then give Glide's actions other shortcuts."),
            Toggle(settings.OverrideWindowsShortcuts, on => { settings.OverrideWindowsShortcuts = on; Apply(); }));

        Section(T("Mouse and touchpad"));
        var wheel = Choice([T("Always scrolls the strip"), T("Scrolls the strip while Win is held"), T("Scrolls the app, as usual")], (int)settings.HorizontalWheel, 280);
        wheel.SelectionChanged += (_, _) =>
        {
            settings.HorizontalWheel = (WheelMode)Math.Max(wheel.SelectedIndex, 0);
            Apply();
        };
        Card(T("Horizontal wheel"),
            T("Tilting the wheel, or a thumb wheel like the MX Master's, glides along the strip; it snaps to a column when you stop. Win + the vertical wheel always does, for mice without one."), wheel);
        var touchpadSettings = new HyperlinkButton { Content = T("Windows touchpad settings"), NavigateUri = new Uri("ms-settings:devices-touchpad") };
        Card(T("Three-finger swipes"),
            T("Swipe left or right with three fingers on a precision touchpad to glide along the strip. Windows uses three-finger swipes to switch apps too: set them to “Nothing” under Touchpad › Three-finger gestures so only Glide moves."),
            Row(touchpadSettings, Toggle(settings.TrackpadSwipe, on => { settings.TrackpadSwipe = on; Apply(); })));

        Section(T("Appearance"));
        var gap = new Slider { Minimum = 0, Maximum = 48, StepFrequency = 2, Value = settings.Gap, Width = 220 };
        gap.ValueChanged += (_, e) =>
        {
            settings.Gap = e.NewValue;
            Apply();
        };
        Card(T("Gap between windows"), T("Also used around the edge of the screen."), gap);
        Card(T("Slide windows into place"), T("Columns glide instead of jumping."), Toggle(settings.Animate, on => { settings.Animate = on; Apply(); }));

        var colors = new ComboBox { Width = 220, ItemsSource = ringColors.Select(c => c.Name).ToList(), IsEnabled = settings.FocusRing };
        colors.SelectedIndex = Math.Max(Array.FindIndex(ringColors, c => c.Value.Equals(settings.FocusRingColor, StringComparison.OrdinalIgnoreCase)), 0);
        colors.SelectionChanged += (_, _) =>
        {
            settings.FocusRingColor = ringColors[Math.Max(colors.SelectedIndex, 0)].Value;
            Apply();
        };
        var thickness = new Slider { Minimum = 1, Maximum = 12, StepFrequency = 1, Value = settings.FocusRingWidth, Width = 220, IsEnabled = settings.FocusRing };
        thickness.ValueChanged += (_, e) =>
        {
            settings.FocusRingWidth = e.NewValue;
            Apply();
        };
        var ring = Toggle(settings.FocusRing, on =>
        {
            settings.FocusRing = on;
            colors.IsEnabled = on;
            thickness.IsEnabled = on;
            Apply();
        });
        Card(T("Focus ring"), T("A colored ring around the focused window. Works with every app, including ones that draw their own title bar."), ring);
        Card(T("Focus ring color"), null, colors);
        Card(T("Focus ring thickness"), null, thickness);
        var dimAmount = new Slider { Minimum = 0.1, Maximum = 0.7, StepFrequency = 0.05, Value = settings.DimAmount, Width = 220, IsEnabled = settings.DimUnfocused };
        dimAmount.ValueChanged += (_, e) =>
        {
            settings.DimAmount = e.NewValue;
            Apply();
        };
        var dim = Toggle(settings.DimUnfocused, on =>
        {
            settings.DimUnfocused = on;
            dimAmount.IsEnabled = on;
            Apply();
        });
        Card(T("Dim unfocused windows"), T("Darkens the windows behind the focused one on its monitor, so the focused window stands out."), Row(dimAmount, dim));
        Card(T("Show workspace numbers when switching"), T("A brief display of the workspaces in use, near the bottom of the screen."),
             Toggle(settings.ShowWorkspaceDisplay, on => { settings.ShowWorkspaceDisplay = on; Apply(); }));

        Section(T("Administrator"));
        if (host.IsElevated)
        {
            Card(T("Glide is running as administrator"), T("It can tile windows of apps that run as administrator."), null);
        }
        else
        {
            var restart = new Button { Content = T("Restart as administrator") };
            restart.Click += (_, _) => host.RestartAsAdministrator();
            Card(T("Apps running as administrator"),
                T("Windows doesn't let Glide move their windows, and Glide's shortcuts don't work while one is focused. Restart Glide as administrator to manage them too."), restart);
        }

        Section(T("Updates"));
        var status = new TextBlock { VerticalAlignment = VerticalAlignment.Center, Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"] };
        var check = new Button { Content = host.AvailableUpdate is { } ready ? T("Install {0}", ready) : T("Check now") };
        check.Click += async (_, _) =>
        {
            check.IsEnabled = false;
            if (host.AvailableUpdate is not null)
            {
                status.Text = T("Downloading…");
                status.Text = await host.InstallUpdateAsync() ? T("Installing; Glide restarts in a moment.") : T("Couldn't install it; see the log.");
                return;
            }
            status.Text = T("Checking…");
            var found = await host.CheckForUpdateAsync(evenIfOff: true);
            status.Text = found is null ? T("Glide is up to date.") : T("Glide {0} is available.", found);
            check.Content = found is null ? T("Check now") : T("Install {0}", found);
            check.IsEnabled = true;
        };
        Card($"Glide {GlideHost.Version.ToString(3)}",
            T("Glide looks for a newer version twice a day and offers it from the tray icon. Updates are checked against their checksums before they install."),
            Row(status, check, Toggle(settings.CheckForUpdates, on => { settings.CheckForUpdates = on; Apply(); })));
        Card(T("Install updates automatically"),
             T("Install a newer version as soon as Glide finds it, instead of waiting for a click on the tray icon. Glide restarts to finish."),
             Toggle(settings.InstallUpdatesAutomatically, on => { settings.InstallUpdatesAutomatically = on; Apply(); }));

        Section(T("Language"));
        var languageCodes = Strings.Languages.Select(l => l.Code).Prepend("auto").ToList();
        var languageNames = Strings.Languages.Select(l => l.Name).Prepend(T("Automatic")).ToList();
        var language = Choice(languageNames, Math.Max(languageCodes.IndexOf(settings.Language.ToLowerInvariant()), 0), 220);
        language.SelectionChanged += (_, _) =>
        {
            settings.Language = languageCodes[Math.Max(language.SelectedIndex, 0)];
            Apply();
        };
        Card(T("Language"), T("Automatic follows Windows. Glide uses a new choice the next time it starts."), language);

        Section(T("Files"));
        var openSettings = new Button { Content = T("Open settings file") };
        openSettings.Click += (_, _) => GlideHost.OpenInEditor(Settings.FilePath);
        var openLog = new Button { Content = T("Open log") };
        openLog.Click += (_, _) => GlideHost.OpenInEditor(Log.FilePath);
        Card(T("Settings and log"), $"{Settings.FilePath}\n{Log.FilePath}", Row(openSettings, openLog));
        var report = new Button { Content = T("Report a problem…") };
        report.Click += (_, _) => host.ReportProblem();
        Card(T("Something not working?"),
             T("Glide puts its log and settings in a zip, with window titles taken out, and opens a new issue on GitHub for you to describe it and attach the zip. Nothing is sent by itself."),
             report);
    }

    // Shortcuts

    private void RenderShortcuts()
    {
        Header(T("Shortcuts"));
        var keymap = settings.Keymap;

        var reset = new Button { Content = T("Reset all to defaults"), IsEnabled = keymap.Overrides.Count > 0 };
        reset.Click += (_, _) =>
        {
            settings.Shortcuts.Clear();
            Apply();
            notice = (InfoBarSeverity.Success, T("All shortcuts are back to their defaults."));
            Render();
        };
        Page.Children.Add(Text(
            T("Click a shortcut and press the keys you want. Esc cancels; Backspace removes the shortcut. Keys go by position, so Win+[ is the key right of P on any keyboard layout."), secondary: true));
        Page.Children.Add(Row(reset));
        if (notice is var (severity, message))
            Page.Children.Add(new InfoBar { Severity = severity, Message = message, IsOpen = true, Margin = new Thickness(0, 8, 0, 0) });

        foreach (var group in Enum.GetValues<ActionGroup>())
        {
            Section(group.Title());
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
            Content = isRecording ? T("Press keys…") : combo is { } shown ? KeyLabels.Describe(shown) : T("Not set"),
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
            var undo = IconButton("", T("Reset to default"));
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
            GlideAction.ConsumeOrExpelLeft => T("Stacks the window into the column on its left; a window already in a stack moves out to its own column on the left."),
            GlideAction.ConsumeOrExpelRight => T("Stacks the window into the column on its right; a window already in a stack moves out to its own column on the right."),
            GlideAction.ToggleTabbed => T("Shows a stacked column's windows as tabs, one at a time."),
            _ => null,
        };
        if (GlideHost.Unavailable.Contains(action))
            note = T("Coming with the overview. Until then Windows keeps this shortcut.");
        else if (note is null && combo is { } bound && ShortcutPolicy.ReplacedWindowsFeature(bound) is { } feature)
            note = settings.OverrideWindowsShortcuts ? T("Replaces Windows' “{0}”.", feature) : T("Given back to Windows (“{0}”). Choose another shortcut to use it in Glide.", feature);
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
            notice = (InfoBarSeverity.Informational, T("{0} has no shortcut now.", action.Title()));
            Render();
            return;
        }

        switch (ShortcutPolicy.Evaluate(combo))
        {
            case ShortcutPolicy.Verdict.Rejected(var reason):
                notice = (InfoBarSeverity.Error, T("{0} can't be used. {1}", KeyLabels.Describe(combo), reason));
                Render();
                return;
            case ShortcutPolicy.Verdict.Warning(var warning):
                notice = (InfoBarSeverity.Warning, T("{0} is now {1}. {2}", action.Title(), KeyLabels.Describe(combo), warning));
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
            notice = (InfoBarSeverity.Informational, T("{0} was used by {1}, which now has no shortcut.", KeyLabels.Describe(combo), others));
        }
        else if (notice is null && !settings.OverrideWindowsShortcuts && ShortcutPolicy.ReplacedWindowsFeature(combo) is { } feature)
        {
            notice = (InfoBarSeverity.Warning, T("{0} stays with Windows (“{1}”) while “Take over Windows shortcuts” is off.", KeyLabels.Describe(combo), feature));
        }
        Render();
    }

    // Rules

    private void RenderRules()
    {
        Header(T("App rules"));
        Page.Children.Add(Text(
            T("Choose how an app's windows are treated. Automatic tiles standard resizable windows and floats dialogs and fixed-size ones. A window you float or tile by hand keeps your choice."), secondary: true));

        var apps = host.RunningApps();
        var picker = new ComboBox
        {
            IsEditable = true,
            Width = 320,
            PlaceholderText = T("App, e.g. notepad.exe"),
            ItemsSource = apps.Select(a => a.AppId).ToList(),
        };
        var add = new Button { Content = T("Add rule"), Style = (Style)Application.Current.Resources["AccentButtonStyle"] };
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
        Card(T("Add a rule"), T("Pick an app with a window open, or type its executable name."), Row(picker, add));

        if (settings.Rules.Count > 0) Section(T("Rules"));
        foreach (var rule in settings.Rules.ToList())
        {
            var mode = Choice([T("Automatic"), T("Always float"), T("Always tile")], (int)rule.Mode, 150);
            var width = Choice(widthNames, (rule.WidthIndex ?? -1) + 1, 160);
            var workspace = Choice([T("Active workspace"), .. Enumerable.Range(1, WorkspaceSet.Count).Select(n => T("Workspace {0}", n))], (rule.Workspace ?? -1) + 1, 170);
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

            var remove = IconButton("", T("Remove rule"));
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
        var toggle = new ToggleSwitch { IsOn = value, OnContent = T("On"), OffContent = T("Off"), MinWidth = 0 };
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
