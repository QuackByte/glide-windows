using System.Runtime.CompilerServices;

namespace Glide.Core.Tests;

internal static class TestSupport
{
    /// <summary>Tests read Glide's English, whatever language this machine is set to.</summary>
    [ModuleInitializer]
    internal static void InEnglish() => Strings.UseLanguage("en");

    /// <summary>1212 wide so preset maths gives whole numbers with a 12 px gap: (1212 - 12) × ½ - 12 = 588.</summary>
    public static readonly Rect Viewport = new(0, 40, 1212, 800);

    public static WindowId[] W(params long[] ids) => [.. ids.Select(id => new WindowId(id))];

    public static long[] Ids(IEnumerable<WindowId> windows) => [.. windows.Select(w => w.Value)];

    public static long[][] Layout(Strip strip) => [.. strip.Columns.Select(c => Ids(c.Windows))];

    public static Column Col(long[] windows, int focusedRow = 0, int widthIndex = 1, bool isFullWidth = false, bool isTabbed = false, double? customWidth = null) =>
        new(W(windows), focusedRow, widthIndex, isFullWidth, isTabbed, customWidth);

    public static Dictionary<WindowId, Rect> Frames(Workspace ws) => ws.Placements().ToDictionary(p => p.Window, p => p.Frame);

    public static Dictionary<WindowId, Visibility> Visibilities(Workspace ws) => ws.Placements().ToDictionary(p => p.Window, p => p.Visibility);

    public static Dictionary<WindowId, WindowPlacement> PlacementMap(Workspace ws) => ws.Placements().ToDictionary(p => p.Window);

    public static StripSnapshot RoundTrip(StripSnapshot snapshot) => StripSnapshot.FromJson(snapshot.ToJson())!;
}
