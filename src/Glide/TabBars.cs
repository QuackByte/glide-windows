using Glide.Core;
using Glide.Platform;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using static Glide.Core.Strings;

namespace Glide;

/// <summary>
/// The bars above tabbed columns. Tabs can be windows of different apps, so each bar is a small
/// window of Glide's, stacked just above the column's active window. A click on a tab switches
/// to it; the bar itself never takes focus.
/// </summary>
internal sealed class TabBars(GlideHost host)
{
    private sealed record Shown(Window Window, nint Handle)
    {
        public TabBar? Bar { get; set; }
    }

    private readonly List<Shown> windows = [];

    public void Show(IReadOnlyList<TabBar> bars)
    {
        while (windows.Count < bars.Count)
        {
            var window = new Window();
            windows.Add(new Shown(window, Overlay.Prepare(window, topmost: false, activates: false)));
        }
        for (var i = 0; i < windows.Count; i++)
        {
            var shown = windows[i];
            if (i >= bars.Count)
            {
                shown.Window.AppWindow.Hide();
                shown.Bar = null;
                continue;
            }
            var bar = bars[i];
            if (shown.Bar is null || !SameContent(shown.Bar, bar)) shown.Window.Content = Render(bar);
            shown.Bar = bar;
            OverlayWindows.PlaceAbove(shown.Handle, bar.Frame, bar.ActiveWindow);
        }
    }

    public void Hide() => Show([]);

    public void Close()
    {
        foreach (var shown in windows) shown.Window.Close();
        windows.Clear();
    }

    private static bool SameContent(TabBar a, TabBar b) =>
        a.ActiveIndex == b.ActiveIndex && a.IsFocused == b.IsFocused && a.Tabs.SequenceEqual(b.Tabs);

    private Grid Render(TabBar bar)
    {
        var grid = new Grid
        {
            Padding = new Thickness(3),
            ColumnSpacing = 2,
            // Opaque: a translucent bar would show the desktop through it.
            Background = (Brush)Application.Current.Resources["SolidBackgroundFillColorBaseBrush"],
        };
        for (var index = 0; index < bar.Tabs.Count; index++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var (window, title) = bar.Tabs[index];
            var active = index == bar.ActiveIndex;
            var tab = new Button
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                Padding = new Thickness(8, 0, 8, 0),
                CornerRadius = new CornerRadius(5),
                BorderThickness = new Thickness(0),
                Background = active
                    ? bar.IsFocused ? WorkspaceDisplay.Accent() : (Brush)Application.Current.Resources["SubtleFillColorSecondaryBrush"]
                    : (Brush)Application.Current.Resources["SubtleFillColorTransparentBrush"],
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Content = IconImages.Labeled(window, new TextBlock
                {
                    Text = title.Length > 0 ? title : T("Untitled"),
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    FontSize = 12,
                    FontWeight = active ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal,
                    Foreground = active && bar.IsFocused
                        ? (Brush)Application.Current.Resources["TextOnAccentFillColorPrimaryBrush"]
                        : (Brush)Application.Current.Resources[active ? "TextFillColorPrimaryBrush" : "TextFillColorSecondaryBrush"],
                }),
            };
            ToolTipService.SetToolTip(tab, title);
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(tab, T("Tab: {0}", title));
            tab.Click += (_, _) => host.GoTo(window);
            Grid.SetColumn(tab, index);
            grid.Children.Add(tab);
        }
        return grid;
    }
}
