namespace Glide.Core;

/// <summary>One finger on the touchpad, in normalized coordinates (0…1).</summary>
public readonly record struct Touch(double X, double Y);

/// <summary>
/// Recognizes horizontal multi-finger swipes from raw touchpad frames. Vertical swipes with the
/// same finger count are left alone.
/// </summary>
public sealed class SwipeTracker(int fingerCount = 3, double threshold = 0.015, double velocityWindow = 0.08)
{
    public abstract record Event
    {
        public sealed record Began : Event;
        /// <summary>Horizontal movement since the previous event, in touchpad widths (negative is left).</summary>
        public sealed record Changed(double Delta) : Event;
        /// <summary>Fingers lifted or changed count. Velocity in touchpad widths per second.</summary>
        public sealed record Ended(double Velocity) : Event;
    }

    private enum State
    {
        Idle,
        /// <summary>The right number of fingers is down but the direction isn't clear yet.</summary>
        Pending,
        Swiping,
        /// <summary>Ignoring input until every finger lifts (wrong direction, or a swipe just ended).</summary>
        Locked,
    }

    public int FingerCount { get; set; } = fingerCount;
    /// <summary>Distance the fingers must travel before a direction is decided.</summary>
    public double Threshold { get; set; } = threshold;
    /// <summary>Samples within this window before release determine the velocity.</summary>
    public double VelocityWindow { get; set; } = velocityWindow;

    private State state = State.Idle;
    private double startX, startY, lastX;
    private readonly List<(double Time, double X)> samples = [];

    public bool IsSwiping => state == State.Swiping;

    public IReadOnlyList<Event> Update(IReadOnlyList<Touch> touches, double time)
    {
        if (touches.Count != FingerCount)
        {
            var events = new List<Event>();
            switch (state)
            {
                case State.Swiping:
                    events.Add(new Event.Ended(Velocity(time)));
                    // Don't restart while the remaining fingers lift one by one.
                    state = State.Locked;
                    break;
                case State.Pending or State.Idle:
                    // Fingers land one at a time, so fewer than needed just means "not yet".
                    // More than needed is someone else's gesture.
                    state = touches.Count > FingerCount ? State.Locked : State.Idle;
                    break;
            }
            if (touches.Count == 0) state = State.Idle;
            samples.Clear();
            return events;
        }

        var x = touches.Average(t => t.X);
        var y = touches.Average(t => t.Y);

        switch (state)
        {
            case State.Locked:
                return [];
            case State.Idle:
                state = State.Pending;
                (startX, startY) = (x, y);
                return [];
            case State.Pending:
            {
                var dx = x - startX;
                var dy = y - startY;
                if (Math.Abs(dx) >= Threshold && Math.Abs(dx) > Math.Abs(dy) * 1.5)
                {
                    state = State.Swiping;
                    lastX = x;
                    samples.Clear();
                    samples.Add((time, x));
                    return [new Event.Began(), new Event.Changed(dx)];
                }
                if (Math.Abs(dy) >= Threshold && Math.Abs(dy) >= Math.Abs(dx)) state = State.Locked;
                return [];
            }
            default:
            {
                var previous = lastX;
                lastX = x;
                samples.Add((time, x));
                samples.RemoveAll(s => s.Time < time - VelocityWindow);
                return x == previous ? [] : [new Event.Changed(x - previous)];
            }
        }
    }

    private double Velocity(double time)
    {
        var recent = samples.Where(s => s.Time >= time - VelocityWindow).ToList();
        if (recent.Count == 0 || recent[^1].Time - recent[0].Time < 0.01) return 0;
        return (recent[^1].X - recent[0].X) / (recent[^1].Time - recent[0].Time);
    }
}
