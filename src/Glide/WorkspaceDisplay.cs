using Glide.Platform;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Graphics;

namespace Glide;

/// <summary>
/// The brief display near the bottom of the screen when you switch workspaces: the workspaces in
/// use, the active one highlighted, and what happened. Never takes focus.
/// </summary>
internal sealed class WorkspaceDisplay
{
    private const double BadgeSize = 38, BadgeSpacing = 8, Padding = 18, Height = 104;

    private Window? window;
    private DispatcherQueueTimer? hideTimer;

    public void Show(WorkspaceAnnouncement announcement)
    {
        if (window is null) Create();
        var badges = new StackPanel { Orientation = Orientation.Horizontal, Spacing = BadgeSpacing, HorizontalAlignment = HorizontalAlignment.Center };
        foreach (var (number, isActive) in announcement.Workspaces)
        {
            badges.Children.Add(new Border
            {
                Width = BadgeSize,
                Height = BadgeSize,
                CornerRadius = new CornerRadius(9),
                Background = isActive ? Accent() : (Brush)Application.Current.Resources["SubtleFillColorSecondaryBrush"],
                Child = new TextBlock
                {
                    Text = number.ToString(),
                    FontSize = 20,
                    FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Foreground = isActive ? new SolidColorBrush(Colors.White) : (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
                },
            });
        }
        var message = new TextBlock
        {
            Text = announcement.Message,
            HorizontalAlignment = HorizontalAlignment.Center,
            Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
        };
        window!.Content = new StackPanel
        {
            Spacing = 10,
            Padding = new Thickness(Padding, 14, Padding, 14),
            VerticalAlignment = VerticalAlignment.Center,
            Children = { badges, message },
        };

        // Sized from the content's parts: the window isn't laid out yet when it's placed.
        var count = announcement.Workspaces.Count;
        var width = Math.Max(count * BadgeSize + (count - 1) * BadgeSpacing, announcement.Message.Length * 7.5) + 2 * Padding;
        // On the monitor whose workspaces these are.
        var area = announcement.Screen;
        var scale = OverlayWindows.ScaleAt(area);
        var size = new SizeInt32((int)(width * scale), (int)(Height * scale));
        window.AppWindow.MoveAndResize(new RectInt32((int)(area.MidX - size.Width / 2.0), (int)(area.MaxY - size.Height - 72 * scale), size.Width, size.Height));
        window.AppWindow.Show(activateWindow: false);

        hideTimer ??= CreateHideTimer();
        hideTimer.Stop();
        hideTimer.Start();
    }

    public void Close()
    {
        hideTimer?.Stop();
        window?.Close();
        window = null;
    }

    private void Create()
    {
        window = new Window { SystemBackdrop = new DesktopAcrylicBackdrop() };
        Overlay.Prepare(window, topmost: true, activates: false);
    }

    private DispatcherQueueTimer CreateHideTimer()
    {
        var timer = DispatcherQueue.GetForCurrentThread().CreateTimer();
        timer.Interval = TimeSpan.FromMilliseconds(900);
        timer.IsRepeating = false;
        timer.Tick += (_, _) => window?.AppWindow.Hide();
        return timer;
    }

    internal static Brush Accent() => (Brush)Application.Current.Resources["AccentFillColorDefaultBrush"];
}
