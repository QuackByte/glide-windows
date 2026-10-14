using System.Globalization;
using System.Text.RegularExpressions;
using Glide.Core;
using Glide.Platform;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using Windows.Graphics;

namespace Glide;

/// <summary>
/// "What's new": entries from CHANGELOG.md, which is built into Glide. Shown once after an update
/// with what changed since the version last seen, and from the tray menu with every release.
/// </summary>
public sealed partial class WhatsNewWindow : Window
{
    public static Changelog History { get; } = LoadChangelog();

    public WhatsNewWindow(IReadOnlyList<Changelog.Release> releases)
    {
        Title = "What's new in Glide";
        SystemBackdrop = new MicaBackdrop();
        ExtendsContentIntoTitleBar = true;
        AppWindow.SetIcon("Assets/Glide.ico");

        var titleBar = new Grid { Padding = new Thickness(16, 0, 0, 0) };
        titleBar.Children.Add(new TextBlock { Text = Title, VerticalAlignment = VerticalAlignment.Center, Style = Resource<Style>("CaptionTextBlockStyle") });
        SetTitleBar(titleBar);

        var body = new StackPanel { Padding = new Thickness(32, 4, 32, 24) };
        foreach (var release in releases) AddRelease(body, release);

        var done = new Button { Content = "Continue", Style = Resource<Style>("AccentButtonStyle"), HorizontalAlignment = HorizontalAlignment.Right, MinWidth = 120 };
        done.Click += (_, _) => Close();
        var footer = new Border
        {
            Padding = new Thickness(24, 16, 24, 16),
            BorderThickness = new Thickness(0, 1, 0, 0),
            BorderBrush = Resource<Brush>("CardStrokeColorDefaultBrush"),
            Child = done,
        };

        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(48) });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var scroller = new ScrollViewer { Content = body };
        Grid.SetRow(scroller, 1);
        Grid.SetRow(footer, 2);
        root.Children.Add(titleBar);
        root.Children.Add(scroller);
        root.Children.Add(footer);
        Content = root;

        var scale = GetScale();
        AppWindow.Resize(new SizeInt32((int)(600 * scale), (int)(640 * scale)));
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
    }

    private static void AddRelease(StackPanel body, Changelog.Release release)
    {
        body.Children.Add(new TextBlock
        {
            Text = $"Glide {Format(release.Version)}",
            Style = Resource<Style>("SubtitleTextBlockStyle"),
            Margin = new Thickness(0, body.Children.Count == 0 ? 8 : 28, 0, 0),
        });
        if (release.Date is { } date)
        {
            body.Children.Add(new TextBlock
            {
                Text = date.ToString("d MMMM yyyy", CultureInfo.GetCultureInfo("en-GB")),
                Style = Resource<Style>("CaptionTextBlockStyle"),
                Foreground = Resource<Brush>("TextFillColorSecondaryBrush"),
            });
        }
        foreach (var paragraph in release.Summary)
            body.Children.Add(Text(paragraph, new Thickness(0, 10, 0, 0)));
        foreach (var section in release.Sections)
        {
            body.Children.Add(new TextBlock { Text = section.Title, Style = Resource<Style>("BodyStrongTextBlockStyle"), Margin = new Thickness(0, 16, 0, 2) });
            foreach (var entry in section.Entries) body.Children.Add(Bullet(entry));
        }
    }

    private static Grid Bullet(string entry)
    {
        var row = new Grid { Margin = new Thickness(0, 6, 0, 0), ColumnSpacing = 10 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.Children.Add(new TextBlock { Text = "•", Foreground = Resource<Brush>("AccentTextFillColorPrimaryBrush") });
        var text = Text(entry, new Thickness(0));
        Grid.SetColumn(text, 1);
        row.Children.Add(text);
        return row;
    }

    [GeneratedRegex(@"\*\*(?<bold>.+?)\*\*|\[(?<text>[^\]]+)\]\((?<url>[^)\s]+)\)")]
    private static partial Regex Inline();

    /// <summary>The changelog's inline markdown: **bold** and [links](https://…).</summary>
    private static RichTextBlock Text(string markdown, Thickness margin)
    {
        var paragraph = new Paragraph();
        var at = 0;
        foreach (Match match in Inline().Matches(markdown))
        {
            if (match.Index > at) paragraph.Inlines.Add(new Run { Text = markdown[at..match.Index] });
            if (match.Groups["bold"].Success)
            {
                var bold = new Bold();
                bold.Inlines.Add(new Run { Text = match.Groups["bold"].Value });
                paragraph.Inlines.Add(bold);
            }
            else if (Uri.TryCreate(match.Groups["url"].Value, UriKind.Absolute, out var url) && url.Scheme == Uri.UriSchemeHttps)
            {
                var link = new Hyperlink { NavigateUri = url };
                link.Inlines.Add(new Run { Text = match.Groups["text"].Value });
                paragraph.Inlines.Add(link);
            }
            else
            {
                paragraph.Inlines.Add(new Run { Text = match.Groups["text"].Value });
            }
            at = match.Index + match.Length;
        }
        if (at < markdown.Length) paragraph.Inlines.Add(new Run { Text = markdown[at..] });
        var block = new RichTextBlock { Margin = margin, TextWrapping = TextWrapping.Wrap, IsTextSelectionEnabled = false };
        block.Blocks.Add(paragraph);
        return block;
    }

    private static string Format(Version version) => version.Build >= 0 ? version.ToString(3) : version.ToString(2);

    private static T Resource<T>(string key) => (T)Application.Current.Resources[key];

    private static Changelog LoadChangelog()
    {
        try
        {
            using var stream = typeof(WhatsNewWindow).Assembly.GetManifestResourceStream("CHANGELOG.md");
            if (stream is null) return Changelog.Parse("");
            using var reader = new StreamReader(stream);
            return Changelog.Parse(reader.ReadToEnd());
        }
        catch (Exception e)
        {
            Log.Error("Couldn't read the changelog", e);
            return Changelog.Parse("");
        }
    }

    private double GetScale() => WinRT.Interop.WindowNative.GetWindowHandle(this) is var hwnd && hwnd != 0 ? GetDpiForWindow(hwnd) / 96.0 : 1;

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(nint hwnd);
}
