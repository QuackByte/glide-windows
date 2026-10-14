using System.Runtime.InteropServices.WindowsRuntime;
using Glide.Core;
using Glide.Platform;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;

namespace Glide;

/// <summary>Window icons as WinUI images.</summary>
internal static class IconImages
{
    /// <summary>The window's app icon at <paramref name="size"/>, or null when it has none.</summary>
    public static Image? For(WindowId window, double size = 16)
    {
        if (AppIcons.Of(window) is not { } icon) return null;
        var bitmap = new WriteableBitmap(icon.Width, icon.Height);
        using (var stream = bitmap.PixelBuffer.AsStream())
            stream.Write(icon.Pixels, 0, icon.Pixels.Length);
        bitmap.Invalidate();
        return new Image { Source = bitmap, Width = size, Height = size, VerticalAlignment = VerticalAlignment.Center };
    }

    /// <summary>An icon followed by text, for tabs and tile titles.</summary>
    public static StackPanel Labeled(WindowId window, TextBlock text, double size = 16)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
        if (For(window, size) is { } image) row.Children.Add(image);
        row.Children.Add(text);
        return row;
    }
}
