using static Glide.Core.Tests.TestSupport;

namespace Glide.Core.Tests;

public class OverviewTests
{
    /// <summary>
    /// Workspace 1: [1] [2 3 stacked] [4]; workspace 3: [10] [11 12 tabbed]; workspace 1 active,
    /// focus on 2.
    /// </summary>
    private static WorkspaceSet Spaces()
    {
        var set = new WorkspaceSet(Viewport);
        set.Active.Strip = new Strip([Col([1]), Col([2, 3]), Col([4])], focusedColumnIndex: 1);
        set.Activate(2);
        set.Active.Strip = new Strip([Col([10], widthIndex: 3), Col([11, 12], focusedRow: 1, isTabbed: true)]);
        set.Activate(0);
        return set;
    }

    [Fact]
    public void RowsCoverOccupiedWorkspacesAndSelectionStartsOnFocus()
    {
        var overview = new Overview(Spaces());
        Assert.Equal([0, 2], overview.Rows.Select(r => r.Workspace));
        Assert.Equal([588, 588, 588], overview.Rows[0].Widths);
        Assert.Equal([0, 600, 1200], overview.Rows[0].Offsets);
        Assert.Equal(2, overview.Selection);
    }

    [Fact]
    public void AnEmptyActiveWorkspaceStillGetsARow()
    {
        var set = Spaces();
        set.Activate(5);
        var overview = new Overview(set);
        Assert.Equal([0, 2, 5], overview.Rows.Select(r => r.Workspace));
        Assert.Equal(1, overview.Selection); // Nothing to focus on 6, so the first window
    }

    [Fact]
    public void LeftAndRightStepThroughColumns()
    {
        var overview = new Overview(Spaces());
        Assert.True(overview.Move(Overview.Direction.Right));
        Assert.Equal(4, overview.Selection);
        Assert.False(overview.Move(Overview.Direction.Right));
        overview.Move(Overview.Direction.Left);
        overview.Move(Overview.Direction.Left);
        Assert.Equal(1, overview.Selection);
    }

    [Fact]
    public void DownGoesThroughTheStackThenToTheNextWorkspace()
    {
        var overview = new Overview(Spaces());
        overview.Move(Overview.Direction.Down);
        Assert.Equal(3, overview.Selection); // Within the stack
        overview.Move(Overview.Direction.Down);
        // Column 2's center is at 894; workspace 3's columns are centered at 594 (full) and 1494.
        Assert.Equal(10, overview.Selection);
        Assert.False(overview.Move(Overview.Direction.Down));
        overview.Move(Overview.Direction.Right);
        Assert.Equal(12, overview.Selection); // A tabbed column selects its active tab
        Assert.Equal(12, overview.ShownWindow(row: 1, column: 1));
        overview.Move(Overview.Direction.Up);
        Assert.Equal(11, overview.Selection); // Up steps through tabs
        Assert.Equal(11, overview.ShownWindow(row: 1, column: 1)); // The selected tab is the one shown
        overview.Move(Overview.Direction.Up);
        Assert.Equal(4, overview.Selection); // Nearest column on the row above
    }

    [Fact]
    public void MovingSkipsEmptyRows()
    {
        var set = Spaces();
        set.Activate(1);
        var overview = new Overview(set);
        Assert.Equal([0, 1, 2], overview.Rows.Select(r => r.Workspace));
        overview.Select(4);
        overview.Move(Overview.Direction.Down);
        Assert.Equal(12, overview.Selection); // Skips empty workspace 2; the tabbed column is nearest
    }

    [Fact]
    public void SelectOnlyKnownWindows()
    {
        var overview = new Overview(Spaces());
        Assert.True(overview.Select(12));
        Assert.False(overview.Select(99));
        Assert.Equal(12, overview.Selection);
        Assert.Equal(1, overview.Location(12)?.Row);
    }
}

public class OverviewFilterTests
{
    private static Overview Make()
    {
        var set = new WorkspaceSet(Viewport);
        set.Active.Strip = new Strip([Col([1]), Col([2, 3])]);
        set.Activate(1);
        set.Active.Strip = new Strip([Col([4])]);
        set.Activate(0);
        return new Overview(set);
    }

    private static readonly Dictionary<WindowId, string> text = new()
    {
        [1] = "Edge Glide pull request",
        [2] = "Windows Terminal ~/Developer/glide",
        [3] = "Windows Terminal ~/Developer/minimal-watchface",
        [4] = "Outlook Résumé draft",
    };

    [Fact]
    public void EveryWordMustMatchIgnoringCaseAndAccents()
    {
        var overview = Make();
        overview.Filter("terminal GLIDE", text);
        Assert.Equal([2], Ids(overview.Matches!));
        Assert.Equal(2, overview.Selection);
        overview.Filter("resume", text);
        Assert.Equal([4], Ids(overview.Matches!)); // Matches across workspaces, accents ignored
        Assert.False(overview.IsMatch(1));
    }

    [Fact]
    public void ArrowsStepThroughMatchesOnly()
    {
        var overview = Make();
        overview.Filter("glide", text);
        Assert.Equal([1, 2], Ids(overview.Matches!));
        Assert.Equal(1, overview.Selection);
        Assert.True(overview.Move(Overview.Direction.Down));
        Assert.Equal(2, overview.Selection);
        Assert.False(overview.Move(Overview.Direction.Right));
        overview.Move(Overview.Direction.Up);
        Assert.Equal(1, overview.Selection);
        Assert.False(overview.Select(3)); // Can't select a window that doesn't match
    }

    [Fact]
    public void NoMatchSelectsNothingAndClearingRestores()
    {
        var overview = Make();
        overview.Filter("zzz", text);
        Assert.Empty(overview.Matches!);
        Assert.Null(overview.Selection);
        Assert.False(overview.Move(Overview.Direction.Right));
        overview.Filter("", text);
        Assert.Null(overview.Matches);
        Assert.Equal(1, overview.Selection);
    }

    [Fact]
    public void TheSelectionStaysWhileItStillMatches()
    {
        var overview = Make();
        overview.Select(3);
        overview.Filter("terminal", text);
        Assert.Equal(3, overview.Selection);
    }
}
