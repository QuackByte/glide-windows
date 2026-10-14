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
        GlideAction.FocusColumnLeft => "Focus Column Left",
        GlideAction.FocusColumnRight => "Focus Column Right",
        GlideAction.FocusWindowUp => "Focus Window Up",
        GlideAction.FocusWindowDown => "Focus Window Down",
        GlideAction.FocusFirstColumn => "Focus First Column",
        GlideAction.FocusLastColumn => "Focus Last Column",
        GlideAction.MoveColumnLeft => "Move Column Left",
        GlideAction.MoveColumnRight => "Move Column Right",
        GlideAction.MoveWindowUp => "Move Window Up",
        GlideAction.MoveWindowDown => "Move Window Down",
        GlideAction.ConsumeOrExpelLeft => "Stack or Unstack Window Left",
        GlideAction.ConsumeOrExpelRight => "Stack or Unstack Window Right",
        GlideAction.CycleColumnWidth => "Cycle Column Width",
        GlideAction.CycleColumnWidthBackward => "Cycle Column Width Backward",
        GlideAction.ToggleFullWidth => "Toggle Full Width",
        GlideAction.ToggleTabbed => "Toggle Tabbed Column",
        GlideAction.CenterColumn => "Center Column",
        GlideAction.FocusPreviousWorkspace => "Focus Previous Workspace",
        GlideAction.FocusNextWorkspace => "Focus Next Workspace",
        GlideAction.MoveColumnToPreviousWorkspace => "Move Column to Previous Workspace",
        GlideAction.MoveColumnToNextWorkspace => "Move Column to Next Workspace",
        GlideAction.ToggleOverview => "Show or Hide Overview",
        GlideAction.ToggleFloating => "Toggle Floating",
        GlideAction.ToggleTiling => "Enable or Disable Tiling",
        GlideAction.FocusMonitorLeft => "Focus Monitor Left",
        GlideAction.FocusMonitorRight => "Focus Monitor Right",
        GlideAction.MoveColumnToMonitorLeft => "Move Column to Monitor Left",
        GlideAction.MoveColumnToMonitorRight => "Move Column to Monitor Right",
        _ when action.WorkspaceCommand() is ({ } target, var moves) && target is WorkspaceTarget.Index(var index) =>
            moves ? $"Move Column to Workspace {index + 1}" : $"Focus Workspace {index + 1}",
        _ => action.ToString(),
    };

    public static ActionGroup Group(this GlideAction action) => action switch
    {
        GlideAction.FocusColumnLeft or GlideAction.FocusColumnRight or GlideAction.FocusWindowUp or GlideAction.FocusWindowDown
            or GlideAction.FocusFirstColumn or GlideAction.FocusLastColumn => ActionGroup.Focus,
        GlideAction.MoveColumnLeft or GlideAction.MoveColumnRight or GlideAction.MoveWindowUp or GlideAction.MoveWindowDown
            or GlideAction.ConsumeOrExpelLeft or GlideAction.ConsumeOrExpelRight => ActionGroup.Move,
        GlideAction.CycleColumnWidth or GlideAction.CycleColumnWidthBackward or GlideAction.ToggleFullWidth
            or GlideAction.ToggleTabbed or GlideAction.CenterColumn => ActionGroup.Columns,
        GlideAction.ToggleOverview or GlideAction.ToggleFloating or GlideAction.ToggleTiling => ActionGroup.General,
        GlideAction.FocusMonitorLeft or GlideAction.FocusMonitorRight or GlideAction.MoveColumnToMonitorLeft
            or GlideAction.MoveColumnToMonitorRight => ActionGroup.Monitors,
        _ => ActionGroup.Workspaces,
    };

    /// <summary>Where a workspace action goes, and whether it takes the focused column along.</summary>
    public static (WorkspaceTarget Target, bool MovesColumn)? WorkspaceCommand(this GlideAction action) => action switch
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
