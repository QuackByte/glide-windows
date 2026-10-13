using System.Text.Json;
using System.Text.Json.Serialization;

namespace Glide.Core;

/// <summary>Identifies a window. In the app this is the window handle (HWND).</summary>
[JsonConverter(typeof(WindowIdJsonConverter))]
public readonly record struct WindowId(long Value) : IComparable<WindowId>
{
    public static implicit operator WindowId(long value) => new(value);

    public int CompareTo(WindowId other) => Value.CompareTo(other.Value);

    public override string ToString() => Value.ToString();
}

/// <summary>Stores a <see cref="WindowId"/> as a bare number.</summary>
public sealed class WindowIdJsonConverter : JsonConverter<WindowId>
{
    public override WindowId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => new(reader.GetInt64());

    public override void Write(Utf8JsonWriter writer, WindowId value, JsonSerializerOptions options) => writer.WriteNumberValue(value.Value);
}

public enum HorizontalDirection { Left, Right }

public enum VerticalDirection { Up, Down }

public readonly record struct Point(double X, double Y);

public readonly record struct Size(double Width, double Height);

/// <summary>A rectangle with a top-left origin, like screen coordinates on Windows.</summary>
public readonly record struct Rect(double X, double Y, double Width, double Height)
{
    public Rect(Point origin, Size size) : this(origin.X, origin.Y, size.Width, size.Height) { }

    public double MinX => X;
    public double MinY => Y;
    public double MaxX => X + Width;
    public double MaxY => Y + Height;
    public double MidX => X + Width / 2;
    public double MidY => Y + Height / 2;
    public Point Origin => new(X, Y);
    public Size Size => new(Width, Height);

    /// <summary>Whether <paramref name="other"/> lies entirely inside, edges included.</summary>
    public bool Contains(Rect other) =>
        other.MinX >= MinX && other.MaxX <= MaxX && other.MinY >= MinY && other.MaxY <= MaxY;

    /// <summary>The overlap of two rectangles, or null when they don't overlap.</summary>
    public Rect? Intersection(Rect other)
    {
        var left = Math.Max(MinX, other.MinX);
        var top = Math.Max(MinY, other.MinY);
        var right = Math.Min(MaxX, other.MaxX);
        var bottom = Math.Min(MaxY, other.MaxY);
        if (right < left || bottom < top) return null;
        return new Rect(left, top, right - left, bottom - top);
    }

    public Rect Inset(double dx, double dy) => new(X + dx, Y + dy, Width - 2 * dx, Height - 2 * dy);

    public Rect Offset(double dx, double dy) => this with { X = X + dx, Y = Y + dy };

    public override string ToString() => $"({X}, {Y}, {Width}×{Height})";
}

internal static class Rounding
{
    /// <summary>Schoolbook rounding (halves away from zero), as the macOS engine uses.</summary>
    public static double Round(double value) => Math.Round(value, MidpointRounding.AwayFromZero);
}
