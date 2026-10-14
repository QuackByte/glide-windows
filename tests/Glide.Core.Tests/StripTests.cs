using static Glide.Core.Tests.TestSupport;

namespace Glide.Core.Tests;

public class StripTests
{
    private static Strip Make(long[][] columns, int focused = 0) => new(columns.Select(c => Col(c)), focused);

    [Fact]
    public void InsertGoesRightOfFocusedColumnAndTakesFocus()
    {
        var s = new Strip();
        s.Insert(1, widthIndex: 1);
        s.Insert(2, widthIndex: 1);
        s.Focus(1);
        s.Insert(3, widthIndex: 1);
        Assert.Equal([[1], [3], [2]], Layout(s));
        Assert.Equal(3, s.FocusedWindow);
    }

    [Fact]
    public void InsertWithoutFocusKeepsFocusedWindow()
    {
        var s = Make([[1], [2]], focused: 1);
        s.Insert(3, widthIndex: 1, at: 0, focus: false);
        Assert.Equal([[3], [1], [2]], Layout(s));
        Assert.Equal(2, s.FocusedWindow);
    }

    [Fact]
    public void InsertIgnoresDuplicates()
    {
        var s = Make([[1]]);
        s.Insert(1, widthIndex: 1);
        Assert.Equal([[1]], Layout(s));
    }

    [Fact]
    public void RemovingFocusedColumnFocusesLeftNeighbour()
    {
        var s = Make([[1], [2], [3]], focused: 1);
        Assert.True(s.Remove(2));
        Assert.Equal(1, s.FocusedWindow);
    }

    [Fact]
    public void RemovingFirstFocusedColumnFocusesNewFirst()
    {
        var s = Make([[1], [2]], focused: 0);
        s.Remove(1);
        Assert.Equal(2, s.FocusedWindow);
    }

    [Fact]
    public void RemovingColumnLeftOfFocusKeepsFocusedWindow()
    {
        var s = Make([[1], [2], [3]], focused: 2);
        s.Remove(1);
        Assert.Equal(3, s.FocusedWindow);
    }

    [Fact]
    public void RemovingLastWindowEmptiesStrip()
    {
        var s = Make([[1]]);
        s.Remove(1);
        Assert.True(s.IsEmpty);
        Assert.Null(s.FocusedWindow);
        Assert.False(s.Remove(1));
    }

    [Fact]
    public void RemovingWindowFromStackKeepsColumn()
    {
        var s = new Strip([Col([1, 2, 3], focusedRow: 2)]);
        s.Remove(3);
        Assert.Equal([[1, 2]], Layout(s));
        Assert.Equal(2, s.FocusedWindow);
        s.Remove(1);
        Assert.Equal(2, s.FocusedWindow);
    }

    [Fact]
    public void FocusColumnStopsAtEnds()
    {
        var s = Make([[1], [2]]);
        Assert.False(s.FocusColumn(HorizontalDirection.Left));
        Assert.True(s.FocusColumn(HorizontalDirection.Right));
        Assert.Equal(2, s.FocusedWindow);
        Assert.False(s.FocusColumn(HorizontalDirection.Right));
    }

    [Fact]
    public void FocusFirstAndLast()
    {
        var s = Make([[1], [2], [3]], focused: 1);
        Assert.True(s.FocusLastColumn());
        Assert.Equal(3, s.FocusedWindow);
        Assert.False(s.FocusLastColumn());
        Assert.True(s.FocusFirstColumn());
        Assert.Equal(1, s.FocusedWindow);
    }

    [Fact]
    public void FocusWindowWithinColumn()
    {
        var s = Make([[1, 2]]);
        Assert.False(s.FocusWindow(VerticalDirection.Up));
        Assert.True(s.FocusWindow(VerticalDirection.Down));
        Assert.Equal(2, s.FocusedWindow);
        Assert.False(s.FocusWindow(VerticalDirection.Down));
    }

    [Fact]
    public void FocusByWindow()
    {
        var s = Make([[1], [2, 3]]);
        Assert.True(s.Focus(3));
        Assert.Equal(1, s.FocusedColumnIndex);
        Assert.Equal(3, s.FocusedWindow);
        Assert.False(s.Focus(3));
        Assert.False(s.Focus(99));
    }

    [Fact]
    public void MoveColumnSwapsAndKeepsFocus()
    {
        var s = Make([[1], [2], [3]], focused: 0);
        Assert.False(s.MoveColumn(HorizontalDirection.Left));
        Assert.True(s.MoveColumn(HorizontalDirection.Right));
        Assert.Equal([[2], [1], [3]], Layout(s));
        Assert.Equal(1, s.FocusedColumnIndex);
        Assert.Equal(1, s.FocusedWindow);
    }

    [Fact]
    public void MoveWindowWithinColumn()
    {
        var s = Make([[1, 2, 3]]);
        Assert.True(s.MoveWindow(VerticalDirection.Down));
        Assert.Equal([[2, 1, 3]], Layout(s));
        Assert.Equal(1, s.FocusedWindow);
        Assert.True(s.MoveWindow(VerticalDirection.Up));
        Assert.False(s.MoveWindow(VerticalDirection.Up));
    }

    [Fact]
    public void ConsumeIntoLeftColumn()
    {
        var s = Make([[1], [2], [3]], focused: 1);
        Assert.True(s.ConsumeOrExpel(HorizontalDirection.Left));
        Assert.Equal([[1, 2], [3]], Layout(s));
        Assert.Equal(0, s.FocusedColumnIndex);
        Assert.Equal(2, s.FocusedWindow);
    }

    [Fact]
    public void ConsumeIntoRightColumn()
    {
        var s = Make([[1], [2], [3]], focused: 1);
        Assert.True(s.ConsumeOrExpel(HorizontalDirection.Right));
        Assert.Equal([[1], [3, 2]], Layout(s));
        Assert.Equal(1, s.FocusedColumnIndex);
        Assert.Equal(2, s.FocusedWindow);
    }

    [Fact]
    public void ConsumeAtEdgeDoesNothing()
    {
        var s = Make([[1], [2]], focused: 0);
        Assert.False(s.ConsumeOrExpel(HorizontalDirection.Left));
        Assert.Equal([[1], [2]], Layout(s));
    }

    [Fact]
    public void ExpelLeftAndRight()
    {
        var s = new Strip([Col([1]), Col([2, 3], focusedRow: 1, widthIndex: 2)], focusedColumnIndex: 1);
        Assert.True(s.ConsumeOrExpel(HorizontalDirection.Left));
        Assert.Equal([[1], [3], [2]], Layout(s));
        Assert.Equal(3, s.FocusedWindow);
        Assert.Equal(2, s.Columns[1].WidthIndex); // Expelled column keeps the source width

        var t = new Strip([Col([2, 3])]);
        Assert.True(t.ConsumeOrExpel(HorizontalDirection.Right));
        Assert.Equal([[3], [2]], Layout(t));
        Assert.Equal(2, t.FocusedWindow);
    }

    [Fact]
    public void CycleWidthWrapsBothWays()
    {
        var s = Make([[1]]);
        s.CycleWidth(presetCount: 4);
        Assert.Equal(2, s.Columns[0].WidthIndex);
        s.CycleWidth(presetCount: 4);
        s.CycleWidth(presetCount: 4);
        Assert.Equal(0, s.Columns[0].WidthIndex);
        s.CycleWidth(presetCount: 4, forward: false);
        Assert.Equal(3, s.Columns[0].WidthIndex);
    }

    [Fact]
    public void CycleWidthLeavesFullWidthAtStoredPreset()
    {
        var s = Make([[1]]);
        s.ToggleFullWidth();
        Assert.True(s.Columns[0].IsFullWidth);
        s.CycleWidth(presetCount: 4);
        Assert.False(s.Columns[0].IsFullWidth);
        Assert.Equal(1, s.Columns[0].WidthIndex);
    }

    [Fact]
    public void EmptyStripActionsAreNoOps()
    {
        var s = new Strip();
        Assert.False(s.FocusColumn(HorizontalDirection.Right));
        Assert.False(s.FocusWindow(VerticalDirection.Down));
        Assert.False(s.MoveColumn(HorizontalDirection.Left));
        Assert.False(s.MoveWindow(VerticalDirection.Up));
        Assert.False(s.ConsumeOrExpel(HorizontalDirection.Left));
        Assert.False(s.CycleWidth(presetCount: 4));
        Assert.False(s.ToggleFullWidth());
        Assert.False(s.FocusFirstColumn());
    }
}
