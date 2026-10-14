using static Glide.Core.Tests.TestSupport;

namespace Glide.Core.Tests;

public class WorkspaceSetTests
{
    /// <summary>Workspace 1 holds windows 1, 2 and 3 (focus on 2); the rest are empty.</summary>
    private static WorkspaceSet Make()
    {
        var set = new WorkspaceSet(Viewport);
        foreach (var window in W(1, 2, 3)) set.Active.Strip.Insert(window, widthIndex: 1, at: set.Active.Strip.Columns.Count);
        set.Active.Strip.Focus(2);
        return set;
    }

    [Fact]
    public void StartsWithNineEmptyWorkspacesAndTheFirstActive()
    {
        var set = new WorkspaceSet(Viewport);
        Assert.Equal(9, set.Workspaces.Count);
        Assert.Equal(0, set.ActiveIndex);
        Assert.Empty(set.Occupied);
    }

    [Fact]
    public void Activate()
    {
        var set = Make();
        Assert.False(set.Activate(0)); // Already active
        Assert.False(set.Activate(9)); // Out of range
        Assert.True(set.Activate(4));
        Assert.Equal(4, set.ActiveIndex);
        Assert.True(set.Active.Strip.IsEmpty);
        Assert.Equal([1, 2, 3], Ids(set.Workspaces[0].Strip.Windows)); // Other workspaces keep their windows
    }

    [Fact]
    public void ResolvePreviousAndNextStopAtTheEnds()
    {
        var set = Make();
        Assert.Null(set.Resolve(new WorkspaceTarget.Previous()));
        Assert.Equal(1, set.Resolve(new WorkspaceTarget.Next()));
        set.Activate(8);
        Assert.Null(set.Resolve(new WorkspaceTarget.Next()));
        Assert.Equal(2, set.Resolve(new WorkspaceTarget.Index(2)));
        Assert.Null(set.Resolve(new WorkspaceTarget.Index(9)));
    }

    [Fact]
    public void MoveFocusedColumnKeepsTheActiveWorkspace()
    {
        var set = Make();
        Assert.True(set.MoveFocusedColumn(2));
        Assert.Equal(0, set.ActiveIndex);
        Assert.Equal([1, 3], Ids(set.Active.Strip.Windows));
        Assert.Equal(1, set.Active.Strip.FocusedWindow); // Focus falls to the left neighbour
        Assert.Equal([2], Ids(set.Workspaces[2].Strip.Windows));
        Assert.Equal(2, set.Workspaces[2].Strip.FocusedWindow);
        Assert.Equal([0, 2], set.Occupied);
        Assert.False(set.MoveFocusedColumn(0)); // Same workspace
    }

    [Fact]
    public void MovedColumnKeepsStackTabsAndWidth()
    {
        var set = new WorkspaceSet(Viewport);
        set.Active.Strip = new Strip([Col([1, 2], focusedRow: 1, widthIndex: 2, isTabbed: true)]);
        set.MoveFocusedColumn(1);
        Assert.Equal([Col([1, 2], focusedRow: 1, widthIndex: 2, isTabbed: true)], set.Workspaces[1].Strip.Columns);
        Assert.False(set.MoveFocusedColumn(3)); // Nothing left to move
    }

    [Fact]
    public void MovedColumnLandsRightOfTheTargetsFocusedColumn()
    {
        var set = Make();
        set.Activate(1);
        set.Active.Strip.Insert(10, widthIndex: 1);
        set.Active.Strip.Insert(11, widthIndex: 1);
        set.Active.Strip.Focus(10);
        set.Activate(0);
        set.MoveFocusedColumn(1);
        Assert.Equal([10, 2, 11], Ids(set.Workspaces[1].Strip.Windows));
    }

    [Fact]
    public void RemoveFindsTheWindowOnAnyWorkspace()
    {
        var set = Make();
        set.MoveFocusedColumn(5);
        Assert.Equal(5, set.IndexOf(2));
        Assert.Equal(5, set.Remove(2));
        Assert.Null(set.IndexOf(2));
        Assert.Null(set.Remove(2));
    }

    [Fact]
    public void ViewportAndSettingsApplyToEveryWorkspace()
    {
        var set = Make();
        set.Viewport = new Rect(0, 0, 800, 600);
        set.Settings = set.Settings with { Gap = 20 };
        Assert.All(set.Workspaces, w => Assert.True(w.Viewport.Width == 800 && w.Settings.Gap == 20));
    }

    [Fact]
    public void SnapshotRestoresEveryWorkspace()
    {
        var set = Make();
        set.MoveFocusedColumn(3);
        set.Activate(3);
        var decoded = RoundTrip(new StripSnapshot(set, [], [], DateTimeOffset.UnixEpoch));
        Assert.Equal([1, 3, 2], Ids(decoded.AllWindows));

        // Window 3 has closed and window 7 is new: it joins the active workspace.
        var (strips, active) = decoded.RestoreWorkspaces(W(1, 2, 7), widthIndex: 1, presetCount: 4);
        Assert.Equal(3, active);
        Assert.Equal([1], Ids(strips[0].Windows));
        Assert.Equal([2, 7], Ids(strips[3].Windows));
        Assert.Equal(2, strips[3].FocusedWindow);

        var rebuilt = new WorkspaceSet(Viewport);
        rebuilt.Restore(strips, active);
        Assert.Equal(3, rebuilt.ActiveIndex);
        Assert.Equal([1, 2, 7], Ids(rebuilt.AllWindows));
    }

    [Fact]
    public void SnapshotsFromBeforeWorkspacesRestoreIntoTheFirst()
    {
        var strip = new Strip([Col([1]), Col([2], widthIndex: 0)]);
        var snapshot = new StripSnapshot(strip, [], [], DateTimeOffset.UnixEpoch);
        var (strips, active) = snapshot.RestoreWorkspaces(W(1, 2), widthIndex: 1, presetCount: 4);
        Assert.Equal(0, active);
        Assert.Single(strips);
        Assert.Equal([1, 2], Ids(strips[0].Windows));
    }

    [Fact]
    public void WorkspaceActionsAndDefaults()
    {
        Assert.Equal(new WorkspaceTarget.Index(2), GlideAction.FocusWorkspace3.WorkspaceCommand()?.Target);
        Assert.Equal(true, GlideAction.MoveColumnToWorkspace9.WorkspaceCommand()?.MovesColumn);
        Assert.Equal(new WorkspaceTarget.Next(), GlideAction.FocusNextWorkspace.WorkspaceCommand()?.Target);
        Assert.Null(GlideAction.FocusColumnLeft.WorkspaceCommand());
        Assert.Equal(22, GlideActions.All.Count(a => a.Group() == ActionGroup.Workspaces));
        Assert.Equal(new KeyCombo('1', KeyModifiers.Win), Keymap.Defaults[GlideAction.FocusWorkspace1]);
        Assert.Equal(new KeyCombo('9', KeyModifiers.Win | KeyModifiers.Shift), Keymap.Defaults[GlideAction.MoveColumnToWorkspace9]);
        Assert.Equal(Keymap.Defaults.Count, Keymap.Defaults.Values.Distinct().Count()); // No two actions share a default
    }
}

public class DepartureTests
{
    private static WorkspaceSet Make()
    {
        var set = new WorkspaceSet(Viewport);
        set.Active.Strip = new Strip([Col([1]), Col([2], widthIndex: 2, isFullWidth: true), Col([3, 4], isTabbed: true)]);
        return set;
    }

    [Fact]
    public void HiddenWindowReturnsToItsSlotWithItsWidth()
    {
        var set = Make();
        var departure = set.DepartureOf(2)!;
        set.Remove(2);
        Assert.Equal(0, set.Rejoin(2, departure, focus: false));
        Assert.Equal([[1], [2], [3, 4]], Layout(set.Active.Strip));
        Assert.Equal(2, set.Active.Strip.Columns[1].WidthIndex);
        Assert.True(set.Active.Strip.Columns[1].IsFullWidth);
    }

    [Fact]
    public void FirstColumnReturnsFirst()
    {
        var set = Make();
        var departure = set.DepartureOf(1)!;
        set.Remove(1);
        set.Rejoin(1, departure, focus: true);
        Assert.Equal([[1], [2], [3, 4]], Layout(set.Active.Strip));
        Assert.Equal(1, set.Active.Strip.FocusedWindow);
    }

    [Fact]
    public void TabReturnsToItsTabs()
    {
        var set = Make();
        var departure = set.DepartureOf(4)!;
        set.Remove(4);
        set.Rejoin(4, departure, focus: true);
        Assert.Equal([3, 4], Ids(set.Active.Strip.Columns[2].Windows));
        Assert.True(set.Active.Strip.Columns[2].IsTabbed);
        Assert.Equal(4, set.Active.Strip.FocusedWindow);
    }

    [Fact]
    public void ReturnsToItsOwnWorkspace()
    {
        var set = Make();
        set.MoveFocusedColumn(4);
        var departure = set.DepartureOf(1)!;
        set.Remove(1);
        Assert.Equal(4, set.Rejoin(1, departure, focus: false));
        Assert.Equal([1], Ids(set.Workspaces[4].Strip.Windows));
        Assert.Equal(0, set.ActiveIndex);
    }

    [Fact]
    public void FallsBackWhenTheNeighbourIsGone()
    {
        var set = Make();
        var departure = set.DepartureOf(3)!;
        set.Remove(3);
        set.Remove(4);
        set.Remove(2);
        set.Rejoin(3, departure, focus: false);
        Assert.Equal([1, 3], Ids(set.Active.Strip.Windows)); // Lands right of the focused column
    }
}
