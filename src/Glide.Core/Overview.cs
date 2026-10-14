using System.Globalization;

namespace Glide.Core;

/// <summary>
/// A zoomed-out view of every workspace: one row per workspace in use, each showing its columns
/// to scale, and the window the user has selected to jump to.
/// </summary>
public sealed class Overview
{
    /// <param name="Widths">Each column's width in strip pixels, as on screen.</param>
    /// <param name="Offsets">Each column's left edge in strip pixels.</param>
    public sealed record Row(int Workspace, IReadOnlyList<Column> Columns, IReadOnlyList<double> Widths, IReadOnlyList<double> Offsets)
    {
        internal double Center(int column) => Offsets[column] + Widths[column] / 2;
    }

    public enum Direction { Left, Right, Up, Down }

    private readonly List<Row> rows;

    /// <summary>
    /// Rows for workspaces holding windows, plus the active one even when empty. Selection starts
    /// on the focused window.
    /// </summary>
    public Overview(WorkspaceSet spaces)
    {
        rows = [];
        for (var index = 0; index < spaces.Workspaces.Count; index++)
        {
            var workspace = spaces.Workspaces[index];
            if (workspace.Strip.IsEmpty && index != spaces.ActiveIndex) continue;
            rows.Add(new Row(
                index,
                [.. workspace.Strip.Columns.Select(c => c.Clone())],
                [.. Enumerable.Range(0, workspace.Strip.Columns.Count).Select(workspace.Width)],
                workspace.ColumnOffsets()));
        }
        Selection = spaces.Active.Strip.FocusedWindow ?? FirstWindow();
    }

    public IReadOnlyList<Row> Rows => rows;
    public WindowId? Selection { get; private set; }
    /// <summary>What the user typed to narrow the overview down.</summary>
    public string Query { get; private set; } = "";
    /// <summary>Windows matching the query, in reading order; null when there is no query.</summary>
    public IReadOnlyList<WindowId>? Matches { get; private set; }

    private WindowId? FirstWindow() =>
        rows.Where(r => r.Columns.Count > 0).Select(r => (WindowId?)r.Columns[0].FocusedWindow).FirstOrDefault();

    public (int Row, int Column, int Index)? Location(WindowId window)
    {
        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            var columns = rows[rowIndex].Columns;
            for (var columnIndex = 0; columnIndex < columns.Count; columnIndex++)
            {
                var index = columns[columnIndex].windowList.IndexOf(window);
                if (index >= 0) return (rowIndex, columnIndex, index);
            }
        }
        return null;
    }

    /// <summary>
    /// The window a column shows: the selected one if it is in the column, else its focused window
    /// (for a tabbed column, its active tab).
    /// </summary>
    public WindowId ShownWindow(int row, int column)
    {
        var shown = rows[row].Columns[column];
        return Selection is { } selection && shown.Windows.Contains(selection) ? selection : shown.FocusedWindow;
    }

    public bool IsMatch(WindowId window) => Matches?.Contains(window) ?? true;

    /// <summary>
    /// Keeps the windows whose search text (app name and title) contains every word of the query,
    /// ignoring case and accents, and selects the first match unless the selection still matches.
    /// With no match, nothing is selected.
    /// </summary>
    public void Filter(string query, IReadOnlyDictionary<WindowId, string> searchText)
    {
        Query = query;
        var words = query.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0)
        {
            Matches = null;
            Selection ??= FirstWindow();
            return;
        }
        var compare = CultureInfo.InvariantCulture.CompareInfo;
        const CompareOptions options = CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace;
        var found = rows.SelectMany(r => r.Columns.SelectMany(c => c.Windows))
            .Where(window =>
            {
                var text = searchText.GetValueOrDefault(window, "");
                return words.All(word => compare.IndexOf(text, word, options) >= 0);
            })
            .ToList();
        Matches = found;
        if (Selection is { } selection && found.Contains(selection)) return;
        Selection = found.Count > 0 ? found[0] : null;
    }

    public bool Select(WindowId window)
    {
        if (Location(window) is null || !IsMatch(window)) return false;
        Selection = window;
        return true;
    }

    /// <summary>
    /// Left and right step through columns. Up and down step through a stack or tabs first, then
    /// to the next workspace row, landing on the column nearest the current one.
    /// </summary>
    public bool Move(Direction direction)
    {
        if (Matches is { } matches)
        {
            // While filtering, arrows step through the matches in reading order.
            if (matches.Count == 0) return false;
            var current = Selection is { } selected ? matches.ToList().IndexOf(selected) : -1;
            var step = direction is Direction.Left or Direction.Up ? -1 : 1;
            var target = current >= 0 ? current + step : 0;
            if (target < 0 || target >= matches.Count) return false;
            Selection = matches[target];
            return true;
        }
        if (Selection is not { } selection || Location(selection) is not (int rowIndex, int columnIndex, int index)) return false;
        var row = rows[rowIndex];
        switch (direction)
        {
            case Direction.Left or Direction.Right:
            {
                var target = columnIndex + (direction == Direction.Left ? -1 : 1);
                if (target < 0 || target >= row.Columns.Count) return false;
                Selection = row.Columns[target].FocusedWindow;
                return true;
            }
            default:
            {
                var step = direction == Direction.Up ? -1 : 1;
                var windows = row.Columns[columnIndex].Windows;
                if (index + step >= 0 && index + step < windows.Count)
                {
                    Selection = windows[index + step];
                    return true;
                }
                var center = row.Center(columnIndex);
                var target = rowIndex + step;
                while (target >= 0 && target < rows.Count && rows[target].Columns.Count == 0) target += step;
                if (target < 0 || target >= rows.Count) return false;
                var next = rows[target];
                var nearest = Enumerable.Range(0, next.Columns.Count).MinBy(i => Math.Abs(next.Center(i) - center));
                Selection = next.Columns[nearest].FocusedWindow;
                return true;
            }
        }
    }
}
