using Glide.Core;
using Glide.Platform;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.System;
using Rect = Glide.Core.Rect;
using WindowId = Glide.Core.WindowId;

namespace Glide;

/// <summary>
/// A zoomed-out view of every workspace in use, with live previews. Arrows or the pointer pick a
/// window, Enter or a click goes to it (switching workspace if needed), and typing filters by
/// app and title. Esc, a click on the background or the overview shortcut closes it.
/// </summary>
/// <remarks>
/// Previews are DWM thumbnails, which draw above the window's XAML content, so the layout is
/// computed here rather than by XAML: tiles go on a Canvas and the previews inside them.
/// </remarks>
internal sealed class OverviewWindow : Window
{
    private const double Margin = 64, SearchHeight = 44, FooterHeight = 32, RowSpacing = 36, BadgeSize = 40, BadgeGap = 24;
    private const double ColumnSpacing = 20, StackSpacing = 12, TitleHeight = 24, TabStripHeight = 26, TileInset = 4;

    private readonly GlideHost host;
    private readonly nint hwnd;
    private readonly Grid root;
    private readonly Canvas canvas;
    private readonly TextBox search;
    private readonly TextBlock matchCount;
    private readonly Dictionary<WindowId, Thumbnail> thumbnails = [];
    private readonly Dictionary<WindowId, (Button Tile, Rect Preview)> tiles = [];
    private OverviewData? data;
    private Overview? overview;
    private WindowId previousForeground;

    public OverviewWindow(GlideHost host)
    {
        this.host = host;
        SystemBackdrop = new DesktopAcrylicBackdrop();
        canvas = new Canvas();
        search = new TextBox
        {
            PlaceholderText = "Type to filter windows",
            Width = 420,
            Height = SearchHeight,
            FontSize = 18,
            CornerRadius = new CornerRadius(SearchHeight / 2),
            Padding = new Thickness(18, 9, 18, 0),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, Margin, 0, 0),
        };
        search.TextChanged += (_, _) => Filter();
        matchCount = new TextBlock
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, Margin + SearchHeight + 6, 0, 0),
            Foreground = new SolidColorBrush(Colors.White) { Opacity = 0.7 },
        };
        var footer = new TextBlock
        {
            Text = "Type to filter    ← → ↑ ↓ select    Enter go    Esc close",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(0, 0, 0, Margin - 24),
            Foreground = new SolidColorBrush(Colors.White) { Opacity = 0.75 },
        };
        root = new Grid
        {
            // Dims what's behind the acrylic, so the previews stand out.
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(0x90, 0, 0, 0)),
            RequestedTheme = ElementTheme.Dark,
            Children = { canvas, search, matchCount, footer },
        };
        root.Tapped += (_, e) =>
        {
            if (e.OriginalSource == root || e.OriginalSource == canvas) Close(goTo: null);
        };
        root.PreviewKeyDown += OnKeyDown;
        // Typing filters wherever keyboard focus happens to be.
        root.CharacterReceived += (_, e) =>
        {
            if (search.FocusState != FocusState.Unfocused || char.IsControl(e.Character)) return;
            search.Text += e.Character;
            search.SelectionStart = search.Text.Length;
            search.Focus(FocusState.Programmatic);
            e.Handled = true;
        };
        root.SizeChanged += (_, _) => Layout();
        Content = root;

        AppWindow.SetPresenter(AppWindowPresenterKind.FullScreen);
        AppWindow.IsShownInSwitchers = false;
        hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        OverlayWindows.Configure(hwnd, activates: true);
        Activated += (_, e) =>
        {
            // Clicking another monitor or Alt+Tab away closes it, like Task View.
            if (e.WindowActivationState == WindowActivationState.Deactivated && IsOpen) Close(goTo: null);
        };
    }

    public bool IsOpen { get; private set; }

    public void Open()
    {
        data = host.MakeOverview();
        if (data is null) return;
        overview = data.Overview;
        previousForeground = GlideHost.ForegroundWindow;
        IsOpen = true;
        host.OverviewOpen = true;
        search.Text = "";
        AppWindow.Show();
        Activate();
        GlideHost.BringToFront(hwnd);
        Layout();
        DispatcherQueue.TryEnqueue(() => search.Focus(FocusState.Programmatic));
    }

    /// <summary>Closes the overview, then goes to <paramref name="goTo"/> if one was chosen.</summary>
    public void Close(WindowId? goTo)
    {
        if (!IsOpen) return;
        IsOpen = false;
        host.OverviewOpen = false;
        ClearPreviews();
        AppWindow.Hide();
        if (goTo is { } target)
            host.GoTo(target);
        else if (previousForeground != default)
            GlideHost.Focus(previousForeground);
    }

    private void ClearPreviews()
    {
        foreach (var thumbnail in thumbnails.Values) thumbnail.Dispose();
        thumbnails.Clear();
        tiles.Clear();
        canvas.Children.Clear();
    }

    private void OnKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (overview is null) return;
        switch (e.Key)
        {
            case VirtualKey.Left: overview.Move(Overview.Direction.Left); break;
            case VirtualKey.Right: overview.Move(Overview.Direction.Right); break;
            case VirtualKey.Up: overview.Move(Overview.Direction.Up); break;
            case VirtualKey.Down: overview.Move(Overview.Direction.Down); break;
            case VirtualKey.Enter:
                if (overview.Selection is { } selection) Close(selection);
                break;
            case VirtualKey.Escape:
                // Esc clears a filter first, then closes.
                if (search.Text.Length > 0) search.Text = "";
                else Close(goTo: null);
                break;
            default:
                return;
        }
        e.Handled = true;
        Restyle();
    }

    private void Filter()
    {
        if (overview is null || data is null) return;
        overview.Filter(search.Text, data.SearchText);
        Restyle();
    }

    /// <summary>Lays out rows of tiles to scale, fitting the screen, and starts each preview.</summary>
    private void Layout()
    {
        if (!IsOpen || overview is null || data is null || root.ActualWidth <= 0) return;
        ClearPreviews();
        var pixels = root.XamlRoot?.RasterizationScale ?? 1;
        var width = root.ActualWidth;
        var top = Margin + SearchHeight + 36;
        var bottom = root.ActualHeight - Margin - FooterHeight;
        var rows = overview.Rows;
        var count = Math.Max(rows.Count, 1);

        // Strip pixels to XAML units: rows as tall as fits, then narrower if a row is too wide.
        var rowHeight = Math.Clamp((bottom - top - RowSpacing * (count - 1)) / count, 60, root.ActualHeight * 0.34);
        var scale = rowHeight / data.Viewport.Height;
        var available = width - 2 * Margin - BadgeSize - BadgeGap;
        var widest = rows.Select(r => r.Widths.Sum() * scale + ColumnSpacing * Math.Max(r.Columns.Count - 1, 0)).DefaultIfEmpty(0).Max();
        if (widest > available)
        {
            var shrink = (available - ColumnSpacing * rows.Max(r => Math.Max(r.Columns.Count - 1, 0))) / (widest - ColumnSpacing * rows.Max(r => Math.Max(r.Columns.Count - 1, 0)));
            scale *= Math.Max(shrink, 0.05);
            rowHeight = data.Viewport.Height * scale;
        }

        var totalHeight = rowHeight * count + RowSpacing * (count - 1);
        var y = top + Math.Max((bottom - top - totalHeight) / 2, 0);
        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++, y += rowHeight + RowSpacing)
        {
            var row = rows[rowIndex];
            AddBadge(row.Workspace, row.Workspace == data.ActiveWorkspace, Margin, y + rowHeight / 2 - BadgeSize / 2);
            var x = Margin + BadgeSize + BadgeGap;
            if (row.Columns.Count == 0)
            {
                AddEmpty(x, y, data.Viewport.Width * scale / 2, rowHeight);
                continue;
            }
            for (var column = 0; column < row.Columns.Count; column++)
            {
                var columnWidth = row.Widths[column] * scale;
                var windows = row.Columns[column].Windows;
                if (row.Columns[column].IsTabbed)
                {
                    AddTabStrip(rowIndex, column, x, y, columnWidth);
                    AddTile(overview.ShownWindow(rowIndex, column), new Rect(x, y + TabStripHeight + 4, columnWidth, rowHeight - TabStripHeight - 4), pixels);
                }
                else
                {
                    var tileHeight = (rowHeight - StackSpacing * (windows.Count - 1)) / windows.Count;
                    for (var i = 0; i < windows.Count; i++)
                        AddTile(windows[i], new Rect(x, y + i * (tileHeight + StackSpacing), columnWidth, tileHeight), pixels);
                }
                x += columnWidth + ColumnSpacing;
            }
        }
        Restyle();
    }

    private void AddBadge(int workspace, bool active, double x, double y)
    {
        var badge = new Border
        {
            Width = BadgeSize,
            Height = BadgeSize,
            CornerRadius = new CornerRadius(10),
            Background = active ? WorkspaceDisplay.Accent() : new SolidColorBrush(Colors.White) { Opacity = 0.12 },
            Child = new TextBlock
            {
                Text = (workspace + 1).ToString(),
                FontSize = 22,
                FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                Foreground = new SolidColorBrush(Colors.White),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            },
        };
        Place(badge, x, y);
    }

    private void AddEmpty(double x, double y, double width, double height)
    {
        var outline = new Rectangle
        {
            Width = width,
            Height = height,
            RadiusX = 10,
            RadiusY = 10,
            Stroke = new SolidColorBrush(Colors.White) { Opacity = 0.3 },
            StrokeThickness = 1.5,
            StrokeDashArray = [6, 5],
        };
        Place(outline, x, y);
        var label = new TextBlock { Text = "Empty", Foreground = new SolidColorBrush(Colors.White) { Opacity = 0.6 } };
        Place(label, x + width / 2 - 20, y + height / 2 - 10);
    }

    private void AddTabStrip(int row, int column, double x, double y, double width)
    {
        var windows = overview!.Rows[row].Columns[column].Windows;
        var strip = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Width = width, Height = TabStripHeight };
        foreach (var window in windows)
        {
            var title = data!.Titles.GetValueOrDefault(window, "");
            var tab = new Button
            {
                Content = new TextBlock { Text = title, TextTrimming = TextTrimming.CharacterEllipsis, FontSize = 11 },
                MaxWidth = Math.Max(width / windows.Count - 4, 24),
                Height = TabStripHeight,
                Padding = new Thickness(6, 0, 6, 0),
                Tag = window,
                IsTabStop = false,
            };
            tab.Click += (_, _) => Close(window);
            strip.Children.Add(tab);
        }
        Place(strip, x, y);
    }

    private void AddTile(WindowId window, Rect frame, double pixels)
    {
        var title = data!.Titles.GetValueOrDefault(window, "");
        var tile = new Button
        {
            Width = frame.Width,
            Height = frame.Height,
            Padding = new Thickness(0),
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(3),
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Bottom,
            Background = new SolidColorBrush(Colors.White) { Opacity = 0.08 },
            // Keyboard focus stays in the search box; arrows move the selection instead.
            IsTabStop = false,
            Content = new TextBlock
            {
                Text = title,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(8, 0, 8, 3),
                FontSize = 12,
                Foreground = new SolidColorBrush(Colors.White),
            },
        };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(tile, title);
        tile.Click += (_, _) => Close(window);
        tile.PointerEntered += (_, _) =>
        {
            if (overview!.Select(window)) Restyle();
        };
        Place(tile, frame.X, frame.Y);

        var preview = new Rect(frame.X + TileInset, frame.Y + TileInset, frame.Width - 2 * TileInset, Math.Max(frame.Height - TitleHeight - TileInset, 1));
        tiles[window] = (tile, preview);
        if (Thumbnail.Create(hwnd, window) is { } thumbnail)
        {
            thumbnails[window] = thumbnail;
            thumbnail.Show(new Rect(preview.X * pixels, preview.Y * pixels, preview.Width * pixels, preview.Height * pixels));
        }
    }

    private void Place(FrameworkElement element, double x, double y)
    {
        Canvas.SetLeft(element, x);
        Canvas.SetTop(element, y);
        canvas.Children.Add(element);
    }

    /// <summary>Highlights the selection and dims windows that don't match the filter.</summary>
    private void Restyle()
    {
        if (overview is null) return;
        var pixels = root.XamlRoot?.RasterizationScale ?? 1;
        foreach (var (window, (tile, preview)) in tiles)
        {
            var match = overview.IsMatch(window);
            tile.BorderBrush = overview.Selection == window ? WorkspaceDisplay.Accent() : new SolidColorBrush(Colors.Transparent);
            tile.Opacity = match ? 1 : 0.25;
            if (thumbnails.TryGetValue(window, out var thumbnail))
                thumbnail.Show(new Rect(preview.X * pixels, preview.Y * pixels, preview.Width * pixels, preview.Height * pixels), match ? (byte)255 : (byte)60);
        }
        matchCount.Text = overview.Matches switch
        {
            null => "",
            { Count: 0 } => "No match",
            { Count: 1 } => "1 match",
            var matches => $"{matches.Count} matches",
        };
    }
}
