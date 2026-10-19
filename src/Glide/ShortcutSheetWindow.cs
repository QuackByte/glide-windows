using Glide.Core;
using Glide.Platform;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.Graphics;
using Windows.System;
using static Glide.Core.Strings;

namespace Glide;

/// <summary>
/// Every shortcut on one page, by group, as they're set now. Opened with its own shortcut
/// (Win+Ctrl+/ by default) or from the tray menu; the same shortcut or Esc closes it.
/// </summary>
public sealed partial class ShortcutSheetWindow : Window
{
    public ShortcutSheetWindow(GlideHost host)
    {
        Title = T("Glide Shortcuts");
        SystemBackdrop = new MicaBackdrop();
        ExtendsContentIntoTitleBar = true;
        AppWindow.SetIcon("Assets/Glide.ico");

        var titleBar = new Grid { Padding = new Thickness(16, 0, 0, 0) };
        titleBar.Children.Add(new TextBlock { Text = Title, VerticalAlignment = VerticalAlignment.Center, Style = Resource<Style>("CaptionTextBlockStyle") });
        SetTitleBar(titleBar);

        // Two columns, the groups split so both are about as long.
        var groups = host.ShortcutList().Select(g => (g.Group, Rows: Rows(g.Actions))).Where(g => g.Rows.Count > 0).ToList();
        var columns = new Grid { ColumnSpacing = 40, Padding = new Thickness(32, 4, 32, 24) };
        columns.ColumnDefinitions.Add(new ColumnDefinition());
        columns.ColumnDefinitions.Add(new ColumnDefinition());
        var halves = new[] { new StackPanel(), new StackPanel() };
        var total = groups.Sum(g => g.Rows.Count + 2);
        var filled = 0;
        foreach (var (group, rows) in groups)
        {
            var half = filled < total / 2 ? halves[0] : halves[1];
            filled += rows.Count + 2;
            half.Children.Add(new TextBlock
            {
                Text = group,
                Style = Resource<Style>("BodyStrongTextBlockStyle"),
                Margin = new Thickness(0, half.Children.Count == 0 ? 8 : 24, 0, 4),
            });
            foreach (var (title, keys) in rows) half.Children.Add(Row(title, keys));
        }
        Grid.SetColumn(halves[1], 1);
        columns.Children.Add(halves[0]);
        columns.Children.Add(halves[1]);

        var hint = new TextBlock
        {
            Text = T("Change them in Settings › Shortcuts."),
            Style = Resource<Style>("CaptionTextBlockStyle"),
            Foreground = Resource<Brush>("TextFillColorSecondaryBrush"),
            Margin = new Thickness(32, 0, 32, 16),
        };

        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(48) });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var scroller = new ScrollViewer { Content = columns };
        Grid.SetRow(scroller, 1);
        Grid.SetRow(hint, 2);
        root.Children.Add(titleBar);
        root.Children.Add(scroller);
        root.Children.Add(hint);
        root.KeyDown += (_, e) =>
        {
            if (e.Key != VirtualKey.Escape) return;
            e.Handled = true;
            Close();
        };
        Content = root;

        var scale = GetScale();
        AppWindow.Resize(new SizeInt32((int)(820 * scale), (int)(720 * scale)));
        if (DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary) is { } display)
        {
            var area = display.WorkArea;
            AppWindow.Move(new PointInt32(area.X + (area.Width - AppWindow.Size.Width) / 2, area.Y + (area.Height - AppWindow.Size.Height) / 2));
        }
        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsMinimizable = false;
            presenter.IsMaximizable = false;
        }
        Activated += (_, _) => root.Focus(FocusState.Programmatic);
    }

    /// <summary>
    /// The bound actions, as title and keys. "Focus Workspace 1…9" and "Move Window to Workspace 1…9"
    /// take one row each when their shortcuts follow the number row, as they do by default.
    /// </summary>
    private static List<(string Title, string Keys)> Rows(IReadOnlyList<(GlideAction Action, string Title, string? Shortcut)> actions)
    {
        var rows = new List<(string, string)>();
        foreach (var (first, title) in new[] { (GlideAction.FocusWorkspace1, T("Focus Workspace 1…9")), (GlideAction.MoveColumnToWorkspace1, T("Move Window to Workspace 1…9")) })
        {
            var nine = actions.Where(a => a.Action >= first && a.Action < first + 9).OrderBy(a => a.Action).ToList();
            if (nine.Count != 9 || nine.Any(a => a.Shortcut is null)) continue;
            var prefixes = nine.Select(a => a.Shortcut![..^1]).Distinct().ToList();
            if (prefixes.Count != 1 || !nine.Select((a, i) => a.Shortcut![^1] == (char)('1' + i)).All(same => same)) continue;
            rows.Add((title, $"{prefixes[0]}1…9"));
            actions = [.. actions.Except(nine)];
        }
        rows.InsertRange(0, actions.Where(a => a.Shortcut is not null).Select(a => (a.Title, a.Shortcut!)));
        return rows;
    }

    private static Grid Row(string title, string keys)
    {
        var row = new Grid { Margin = new Thickness(0, 5, 0, 0), ColumnSpacing = 16 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.Children.Add(new TextBlock { Text = title, TextWrapping = TextWrapping.Wrap });
        var shortcut = new TextBlock { Text = keys, Foreground = Resource<Brush>("TextFillColorSecondaryBrush"), HorizontalAlignment = HorizontalAlignment.Right };
        Grid.SetColumn(shortcut, 1);
        row.Children.Add(shortcut);
        return row;
    }

    private static T Resource<T>(string key) => (T)Application.Current.Resources[key];

    private double GetScale() => WinRT.Interop.WindowNative.GetWindowHandle(this) is var hwnd && hwnd != 0 ? GetDpiForWindow(hwnd) / 96.0 : 1;

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(nint hwnd);
}
