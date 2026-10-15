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
using static Glide.Core.Strings;

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
    /// <summary>Where each tile sits before its row scrolls, by window, and its row.</summary>
    private readonly Dictionary<WindowId, (int Row, Rect Frame)> unscrolled = [];
    /// <summary>How far each row is scrolled, in XAML units; rows too wide for the screen scroll.</summary>
    private double[] rowOffsets = [];
    private double[] rowWidths = [];
    private (double Left, double Right) visibleColumns;
    private (double Top, double Height)[] rowBands = [];
    /// <summary>The canvas the row being built goes into, clipped to the visible part of the row.</summary>
    private Canvas? rowCanvas;
    /// <summary>Tiles never shrink below this share of their natural size; rows scroll instead.</summary>
    private const double MinimumShrink = 0.45;
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
            PlaceholderText = T("Type to filter windows"),
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
            Text = T("Type to filter    ← → ↑ ↓ select    Enter go    Esc close"),
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
        root.PointerWheelChanged += OnWheel;
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
        rowOffsets = new double[overview.Rows.Count];
        AppWindow.Show();
        Activate();
        GlideHost.BringToFront(hwnd);
        Layout();
        RevealSelection();
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
        unscrolled.Clear();
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
        if (!RevealSelection()) Restyle();
    }

    /// <summary>The wheel scrolls the row under the pointer, if it's wider than the screen.</summary>
    private void OnWheel(object sender, PointerRoutedEventArgs e)
    {
        var point = e.GetCurrentPoint(root);
        var delta = point.Properties.MouseWheelDelta;
        if (point.Properties.IsHorizontalMouseWheel) delta = -delta;
        for (var row = 0; row < rowBands.Length; row++)
        {
            var (top, height) = rowBands[row];
            if (point.Position.Y < top || point.Position.Y > top + height) continue;
            if (ScrollRow(row, rowOffsets[row] - delta)) Layout();
            e.Handled = true;
            return;
        }
    }

    private bool ScrollRow(int row, double offset)
    {
        var maximum = Math.Max(rowWidths[row] - (visibleColumns.Right - visibleColumns.Left), 0);
        var clamped = Math.Clamp(offset, 0, maximum);
        if (Math.Abs(clamped - rowOffsets[row]) < 0.5) return false;
        rowOffsets[row] = clamped;
        return true;
    }

    /// <summary>Scrolls the selection's row so the selected tile is on screen; returns true if it laid out again.</summary>
    private bool RevealSelection()
    {
        if (overview?.Selection is not { } selection || !unscrolled.TryGetValue(selection, out var tile)) return false;
        var width = visibleColumns.Right - visibleColumns.Left;
        var left = tile.Frame.X - visibleColumns.Left;
        var offset = rowOffsets[tile.Row];
        if (left - offset < 0) offset = left - ColumnSpacing;
        else if (left + tile.Frame.Width - offset > width) offset = left + tile.Frame.Width - width + ColumnSpacing;
        if (!ScrollRow(tile.Row, offset)) return false;
        Layout();
        return true;
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
            // Shrink to fit, but not so far that windows become unreadable: past that, rows scroll.
            var gaps = ColumnSpacing * rows.Max(r => Math.Max(r.Columns.Count - 1, 0));
            var shrink = (available - gaps) / Math.Max(widest - gaps, 1);
            scale *= Math.Clamp(shrink, MinimumShrink, 1);
            rowHeight = data.Viewport.Height * scale;
        }
        visibleColumns = (Margin + BadgeSize + BadgeGap, width - Margin);
        rowWidths = [.. rows.Select(r => r.Widths.Sum() * scale + ColumnSpacing * Math.Max(r.Columns.Count - 1, 0))];
        if (rowOffsets.Length != rows.Count) rowOffsets = new double[rows.Count];
        rowBands = new (double, double)[rows.Count];

        var totalHeight = rowHeight * count + RowSpacing * (count - 1);
        var y = top + Math.Max((bottom - top - totalHeight) / 2, 0);
        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++, y += rowHeight + RowSpacing)
        {
            var row = rows[rowIndex];
            rowBands[rowIndex] = (y, rowHeight);
            AddBadge(row.Workspace, row.Workspace == data.ActiveWorkspace, Margin, y + rowHeight / 2 - BadgeSize / 2);
            var x = Margin + BadgeSize + BadgeGap;
            // Each row draws into its own canvas, clipped to the part of the screen it scrolls in.
            rowCanvas = new Canvas
            {
                Clip = new RectangleGeometry { Rect = new Windows.Foundation.Rect(visibleColumns.Left - 6, y - 8, visibleColumns.Right - visibleColumns.Left + 12, rowHeight + 16) },
            };
            canvas.Children.Add(rowCanvas);
            var shift = rowOffsets[rowIndex];
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
                    AddTabStrip(rowIndex, column, x - shift, y, columnWidth);
                    AddTile(rowIndex, overview.ShownWindow(rowIndex, column), new Rect(x, y + TabStripHeight + 4, columnWidth, rowHeight - TabStripHeight - 4), shift, pixels);
                }
                else
                {
                    var tileHeight = (rowHeight - StackSpacing * (windows.Count - 1)) / windows.Count;
                    for (var i = 0; i < windows.Count; i++)
                        AddTile(rowIndex, windows[i], new Rect(x, y + i * (tileHeight + StackSpacing), columnWidth, tileHeight), shift, pixels);
                }
                x += columnWidth + ColumnSpacing;
            }
            rowCanvas = null;
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
        var label = new TextBlock { Text = T("Empty"), Foreground = new SolidColorBrush(Colors.White) { Opacity = 0.6 } };
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
                Content = IconImages.Labeled(window, new TextBlock { Text = title, TextTrimming = TextTrimming.CharacterEllipsis, FontSize = 11 }, size: 14),
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

    private void AddTile(int row, WindowId window, Rect unscrolledFrame, double shift, double pixels)
    {
        unscrolled[window] = (row, unscrolledFrame);
        var frame = unscrolledFrame.Offset(-shift, 0);
        // Tiles scrolled out of view aren't built at all.
        if (frame.MaxX < visibleColumns.Left || frame.MinX > visibleColumns.Right) return;
        var title = data!.Titles.GetValueOrDefault(window, "");
        var tile = new Button
        {
            Width = frame.Width,
            Height = frame.Height,
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(3),
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Bottom,
            Background = new SolidColorBrush(Colors.White) { Opacity = 0.08 },
            // Keyboard focus stays in the search box; arrows move the selection instead.
            IsTabStop = false,
            Content = IconImages.Labeled(window, new TextBlock
            {
                Text = title,
                TextTrimming = TextTrimming.CharacterEllipsis,
                FontSize = 12,
                Foreground = new SolidColorBrush(Colors.White),
            }),
            Padding = new Thickness(8, 0, 8, 3),
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
            thumbnail.Show(ToPixels(preview, pixels), clip: VisibleBand(pixels));
        }
    }

    private void Place(FrameworkElement element, double x, double y)
    {
        Canvas.SetLeft(element, x);
        Canvas.SetTop(element, y);
        (rowCanvas ?? canvas).Children.Add(element);
    }

    private static Rect ToPixels(Rect rect, double pixels) => new(rect.X * pixels, rect.Y * pixels, rect.Width * pixels, rect.Height * pixels);

    /// <summary>Where previews may draw, in pixels: DWM draws above XAML, so its clipping is done by hand.</summary>
    private Rect VisibleBand(double pixels) =>
        new(visibleColumns.Left * pixels, 0, (visibleColumns.Right - visibleColumns.Left) * pixels, root.ActualHeight * pixels);

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
                thumbnail.Show(ToPixels(preview, pixels), match ? (byte)255 : (byte)60, VisibleBand(pixels));
        }
        matchCount.Text = overview.Matches switch
        {
            null => "",
            { Count: 0 } => T("No match"),
            { Count: 1 } => T("1 match"),
            var matches => T("{0} matches", matches.Count),
        };
    }
}
