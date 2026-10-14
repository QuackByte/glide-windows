using static Glide.Core.Tests.TestSupport;

namespace Glide.Core.Tests;

public class StripSnapshotTests
{
    private static readonly DateTimeOffset boot = DateTimeOffset.FromUnixTimeSeconds(1_000);

    private static Strip SavedStrip() =>
        new([Col([1], widthIndex: 0), Col([2, 3], focusedRow: 1, widthIndex: 2), Col([4], isFullWidth: true)], focusedColumnIndex: 1);

    private static StripSnapshot Snapshot() => new(SavedStrip(), [], [], boot);

    [Fact]
    public void RoundTripKeepsOrderStacksWidthsAndFocus()
    {
        var restored = Snapshot().Restore(W(4, 3, 2, 1), widthIndex: 1, presetCount: 4);
        Assert.Equal(SavedStrip(), restored);
    }

    [Fact]
    public void ClosedWindowsDropOutAndEmptyColumnsDisappear()
    {
        var restored = Snapshot().Restore(W(3, 4), widthIndex: 1, presetCount: 4);
        Assert.Equal([[3], [4]], Layout(restored));
        Assert.Equal(2, restored.Columns[0].WidthIndex); // Column keeps its width without its first window
        Assert.True(restored.Columns[1].IsFullWidth);
        Assert.Equal(3, restored.FocusedWindow);
    }

    [Fact]
    public void FocusedRowFollowsTheWindowWhenAnotherCloses()
    {
        var restored = Snapshot().Restore(W(1, 3, 4), widthIndex: 1, presetCount: 4);
        Assert.Equal([3], Ids(restored.Columns[1].Windows));
        Assert.Equal(0, restored.Columns[1].FocusedRow);
    }

    [Fact]
    public void UnknownWindowsAppendInGivenOrder()
    {
        var restored = Snapshot().Restore(W(9, 1, 8), widthIndex: 1, presetCount: 4);
        Assert.Equal([[1], [9], [8]], Layout(restored));
        Assert.Equal(1, restored.Columns[1].WidthIndex);
    }

    [Fact]
    public void OutOfRangeWidthFallsBackToDefault()
    {
        var snapshot = new StripSnapshot(new Strip([Col([1], widthIndex: 7)]), [], [], boot);
        Assert.Equal(1, snapshot.Restore(W(1), widthIndex: 1, presetCount: 4).Columns[0].WidthIndex);
    }

    [Fact]
    public void JsonRoundTrip()
    {
        var snapshot = new StripSnapshot(SavedStrip(), W(7), W(8), boot);
        Assert.Equal(snapshot, RoundTrip(snapshot));
    }
}
