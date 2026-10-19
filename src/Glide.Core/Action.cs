using static Glide.Core.Strings;

namespace Glide.Core;

/// <summary>Everything a shortcut can trigger. Names are persisted; do not rename them.</summary>
public enum GlideAction
{
    FocusColumnLeft,
    FocusColumnRight,
    FocusWindowUp,
    FocusWindowDown,
    FocusFirstColumn,
    FocusLastColumn,
    MoveColumnLeft,
    MoveColumnRight,
    MoveWindowUp,
    MoveWindowDown,
    ConsumeOrExpelLeft,
    ConsumeOrExpelRight,
    CycleColumnWidth,
    CycleColumnWidthBackward,
    ToggleFullWidth,
    ToggleTabbed,
    CenterColumn,
    FocusPreviousWorkspace,
    FocusNextWorkspace,
    MoveColumnToPreviousWorkspace,
    MoveColumnToNextWorkspace,
    FocusWorkspace1,
    FocusWorkspace2,
    FocusWorkspace3,
    FocusWorkspace4,
    FocusWorkspace5,
    FocusWorkspace6,
    FocusWorkspace7,
    FocusWorkspace8,
    FocusWorkspace9,
    MoveColumnToWorkspace1,
    MoveColumnToWorkspace2,
    MoveColumnToWorkspace3,
    MoveColumnToWorkspace4,
    MoveColumnToWorkspace5,
    MoveColumnToWorkspace6,
    MoveColumnToWorkspace7,
    MoveColumnToWorkspace8,
    MoveColumnToWorkspace9,
    ToggleOverview,
    ToggleFloating,
    ToggleTiling,
    FocusMonitorLeft,
    FocusMonitorRight,
    MoveColumnToMonitorLeft,
    MoveColumnToMonitorRight,
    ShowShortcuts,
    ToggleLayout,
    CycleTiledColumns,
}

public enum ActionGroup { Focus, Move, Columns, Workspaces, Monitors, General }

/// <summary>Where a workspace action goes.</summary>
public abstract record WorkspaceTarget
{
    /// <summary>Zero-based: workspace 1 is index 0.</summary>
    public sealed record Index(int Value) : WorkspaceTarget;
    public sealed record Previous : WorkspaceTarget;
    public sealed record Next : WorkspaceTarget;
}

public static class GlideActions
{
    public static IReadOnlyList<GlideAction> All { get; } = Enum.GetValues<GlideAction>();

    public static string Title(this GlideAction action) => action switch
    {
        GlideAction.FocusColumnLeft => T("Focus Column Left"),
        GlideAction.FocusColumnRight => T("Focus Column Right"),
        GlideAction.FocusWindowUp => T("Focus Window Up"),
        GlideAction.FocusWindowDown => T("Focus Window Down"),
        GlideAction.FocusFirstColumn => T("Focus First Column"),
        GlideAction.FocusLastColumn => T("Focus Last Column"),
        GlideAction.MoveColumnLeft => T("Move Column Left"),
        GlideAction.MoveColumnRight => T("Move Column Right"),
        GlideAction.MoveWindowUp => T("Move Window Up"),
        GlideAction.MoveWindowDown => T("Move Window Down"),
        GlideAction.ConsumeOrExpelLeft => T("Stack or Unstack Window Left"),
        GlideAction.ConsumeOrExpelRight => T("Stack or Unstack Window Right"),
        GlideAction.CycleColumnWidth => T("Cycle Column Width"),
        GlideAction.CycleColumnWidthBackward => T("Cycle Column Width Backward"),
        GlideAction.ToggleFullWidth => T("Toggle Full Width"),
        GlideAction.ToggleTabbed => T("Toggle Tabbed Column"),
        GlideAction.CenterColumn => T("Center Column"),
        GlideAction.FocusPreviousWorkspace => T("Focus Previous Workspace"),
        GlideAction.FocusNextWorkspace => T("Focus Next Workspace"),
        GlideAction.MoveColumnToPreviousWorkspace => T("Move Window to Previous Workspace"),
        GlideAction.MoveColumnToNextWorkspace => T("Move Window to Next Workspace"),
        GlideAction.ToggleOverview => T("Show or Hide Overview"),
        GlideAction.ToggleFloating => T("Toggle Floating"),
        GlideAction.ToggleTiling => T("Enable or Disable Tiling"),
        GlideAction.FocusMonitorLeft => T("Focus Monitor Left"),
        GlideAction.FocusMonitorRight => T("Focus Monitor Right"),
        GlideAction.MoveColumnToMonitorLeft => T("Move Column to Monitor Left"),
        GlideAction.MoveColumnToMonitorRight => T("Move Column to Monitor Right"),
        GlideAction.ShowShortcuts => T("Show All Shortcuts"),
        GlideAction.ToggleLayout => T("Switch Between Scrolling and Tiled"),
        GlideAction.CycleTiledColumns => T("Switch Between 2 and 3 Columns"),
        _ when action.WorkspaceCommand() is ({ } target, var moves) && target is WorkspaceTarget.Index(var index) =>
            moves ? T("Move Window to Workspace {0}", index + 1) : T("Focus Workspace {0}", index + 1),
        _ => action.ToString(),
    };

    public static string Title(this ActionGroup group) => group switch
    {
        ActionGroup.Focus => T("Focus"),
        ActionGroup.Move => T("Move"),
        ActionGroup.Columns => T("Columns"),
        ActionGroup.Workspaces => T("Workspaces"),
        ActionGroup.Monitors => T("Monitors"),
        _ => T("General"),
    };

    public static ActionGroup Group(this GlideAction action) => action switch
    {
        GlideAction.FocusColumnLeft or GlideAction.FocusColumnRight or GlideAction.FocusWindowUp or GlideAction.FocusWindowDown
            or GlideAction.FocusFirstColumn or GlideAction.FocusLastColumn => ActionGroup.Focus,
        GlideAction.MoveColumnLeft or GlideAction.MoveColumnRight or GlideAction.MoveWindowUp or GlideAction.MoveWindowDown
            or GlideAction.ConsumeOrExpelLeft or GlideAction.ConsumeOrExpelRight => ActionGroup.Move,
        GlideAction.CycleColumnWidth or GlideAction.CycleColumnWidthBackward or GlideAction.ToggleFullWidth
            or GlideAction.ToggleTabbed or GlideAction.CenterColumn => ActionGroup.Columns,
        GlideAction.ToggleOverview or GlideAction.ToggleFloating or GlideAction.ToggleTiling or GlideAction.ShowShortcuts => ActionGroup.General,
        GlideAction.FocusMonitorLeft or GlideAction.FocusMonitorRight or GlideAction.MoveColumnToMonitorLeft
            or GlideAction.MoveColumnToMonitorRight => ActionGroup.Monitors,
        _ => ActionGroup.Workspaces,
    };

    /// <summary>Where a workspace action goes, and whether it sends the focused window there (else it just switches).</summary>
    public static (WorkspaceTarget Target, bool MovesWindow)? WorkspaceCommand(this GlideAction action) => action switch
    {
        GlideAction.FocusPreviousWorkspace => (new WorkspaceTarget.Previous(), false),
        GlideAction.FocusNextWorkspace => (new WorkspaceTarget.Next(), false),
        GlideAction.MoveColumnToPreviousWorkspace => (new WorkspaceTarget.Previous(), true),
        GlideAction.MoveColumnToNextWorkspace => (new WorkspaceTarget.Next(), true),
        >= GlideAction.FocusWorkspace1 and <= GlideAction.FocusWorkspace9 =>
            (new WorkspaceTarget.Index(action - GlideAction.FocusWorkspace1), false),
        >= GlideAction.MoveColumnToWorkspace1 and <= GlideAction.MoveColumnToWorkspace9 =>
            (new WorkspaceTarget.Index(action - GlideAction.MoveColumnToWorkspace1), true),
        _ => null,
    };
}
