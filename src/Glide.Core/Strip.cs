namespace Glide.Core;

/// <summary>
/// A column on the strip: one or more windows stacked vertically, sharing one width. A tabbed
/// column shows only its focused window, full height, with the others as tabs.
/// </summary>
public sealed class Column : IEquatable<Column>
{
    internal readonly List<WindowId> windowList;

    public Column(IEnumerable<WindowId> windows, int focusedRow = 0, int widthIndex = 0, bool isFullWidth = false, bool isTabbed = false, double? customWidth = null)
    {
        windowList = [.. windows];
        if (windowList.Count == 0) throw new ArgumentException("A column needs at least one window", nameof(windows));
        FocusedRow = Math.Clamp(focusedRow, 0, windowList.Count - 1);
        WidthIndex = widthIndex;
        IsFullWidth = isFullWidth;
        IsTabbed = isTabbed;
        CustomWidth = customWidth;
    }

    public IReadOnlyList<WindowId> Windows => windowList;
    public int FocusedRow { get; internal set; }
    /// <summary>Index into <see cref="LayoutSettings.WidthPresets"/>.</summary>
    public int WidthIndex { get; internal set; }
    /// <summary>Overrides <see cref="WidthIndex"/> with the full viewport width until toggled off.</summary>
    public bool IsFullWidth { get; internal set; }
    public bool IsTabbed { get; internal set; }
    /// <summary>A width set by resizing a window with the mouse; replaces the preset until the width is cycled.</summary>
    public double? CustomWidth { get; internal set; }

    public WindowId FocusedWindow => windowList[FocusedRow];

    public Column Clone() => new(windowList, FocusedRow, WidthIndex, IsFullWidth, IsTabbed, CustomWidth);

    public bool Equals(Column? other) =>
        other is not null && windowList.SequenceEqual(other.windowList) && FocusedRow == other.FocusedRow && WidthIndex == other.WidthIndex
        && IsFullWidth == other.IsFullWidth && IsTabbed == other.IsTabbed && CustomWidth == other.CustomWidth;

    public override bool Equals(object? obj) => Equals(obj as Column);

    public override int GetHashCode() => HashCode.Combine(windowList.Count, FocusedRow, WidthIndex, IsFullWidth, IsTabbed, CustomWidth);

    public override string ToString() => $"[{string.Join(" ", windowList)}]";
}

/// <summary>The ordered columns of one display and which one has focus. Purely structural: no geometry.</summary>
public sealed class Strip : IEquatable<Strip>
{
    private readonly List<Column> columns;

    public Strip() => columns = [];

    public Strip(IEnumerable<Column> columns, int focusedColumnIndex = 0)
    {
        this.columns = [.. columns];
        FocusedColumnIndex = this.columns.Count == 0 ? 0 : Math.Clamp(focusedColumnIndex, 0, this.columns.Count - 1);
    }

    public IReadOnlyList<Column> Columns => columns;
    public int FocusedColumnIndex { get; private set; }

    public bool IsEmpty => columns.Count == 0;
    public Column? FocusedColumn => IsEmpty ? null : columns[FocusedColumnIndex];
    public WindowId? FocusedWindow => FocusedColumn?.FocusedWindow;
    public IReadOnlyList<WindowId> Windows => [.. columns.SelectMany(c => c.Windows)];

    public bool Contains(WindowId window) => Location(window) is not null;

    public (int Column, int Row)? Location(WindowId window)
    {
        for (var columnIndex = 0; columnIndex < columns.Count; columnIndex++)
        {
            var row = columns[columnIndex].windowList.IndexOf(window);
            if (row >= 0) return (columnIndex, row);
        }
        return null;
    }

    public Strip Clone() => new(columns.Select(c => c.Clone()), FocusedColumnIndex);

    // Membership

    /// <summary>Adds a window in a new column. By default the column goes right of the focused column.</summary>
    public void Insert(WindowId window, int widthIndex, int? at = null, bool focus = true)
    {
        if (Contains(window)) return;
        Insert(new Column([window], widthIndex: widthIndex), at, focus);
    }

    /// <summary>Adds a whole column (e.g. one moved from another workspace). By default it goes right of the focused column.</summary>
    public void Insert(Column column, int? at = null, bool focus = true)
    {
        var target = Math.Clamp(at ?? (IsEmpty ? 0 : FocusedColumnIndex + 1), 0, columns.Count);
        columns.Insert(target, column);
        if (focus || columns.Count == 1)
            FocusedColumnIndex = target;
        else if (target <= FocusedColumnIndex)
            FocusedColumnIndex++;
    }

    /// <summary>Adds a window to the bottom of (or as the last tab in) the column holding <paramref name="sibling"/>.</summary>
    public bool Join(WindowId window, WindowId sibling, bool focus = true)
    {
        if (Contains(window) || Location(sibling) is not (int index, _)) return false;
        columns[index].windowList.Add(window);
        if (focus)
        {
            columns[index].FocusedRow = columns[index].windowList.Count - 1;
            FocusedColumnIndex = index;
        }
        return true;
    }

    /// <summary>Takes the focused column out of the strip; focus moves to its left neighbour.</summary>
    public Column? RemoveFocusedColumn()
    {
        if (IsEmpty) return null;
        var column = columns[FocusedColumnIndex];
        columns.RemoveAt(FocusedColumnIndex);
        FocusedColumnIndex = IsEmpty ? 0 : Math.Max(Math.Min(FocusedColumnIndex - 1, columns.Count - 1), 0);
        return column;
    }

    /// <summary>
    /// Takes just the focused window out. A window alone in its column leaves with the column (and its
    /// width); one that shares a column leaves it to the others, as a new column of
    /// <paramref name="widthIndex"/>. Focus moves as it does when a window closes.
    /// </summary>
    public Column? RemoveFocusedWindow(int widthIndex)
    {
        if (FocusedColumn is not { } column) return null;
        if (column.windowList.Count == 1) return RemoveFocusedColumn();
        var window = column.FocusedWindow;
        Remove(window);
        return new Column([window], widthIndex: widthIndex);
    }

    /// <summary>Removes a window. An emptied column disappears and focus moves to its left neighbour.</summary>
    public bool Remove(WindowId window)
    {
        if (Location(window) is not (int columnIndex, int row)) return false;
        var column = columns[columnIndex];
        column.windowList.RemoveAt(row);
        if (column.windowList.Count == 0)
        {
            columns.RemoveAt(columnIndex);
            if (columnIndex < FocusedColumnIndex || (columnIndex == FocusedColumnIndex && columnIndex > 0))
                FocusedColumnIndex--;
            FocusedColumnIndex = IsEmpty ? 0 : Math.Min(FocusedColumnIndex, columns.Count - 1);
        }
        else if (row < column.FocusedRow || column.FocusedRow >= column.windowList.Count)
        {
            column.FocusedRow = Math.Max(column.FocusedRow - 1, 0);
        }
        return true;
    }

    // Focus

    public bool Focus(WindowId window)
    {
        if (Location(window) is not (int columnIndex, int row)) return false;
        var changed = columnIndex != FocusedColumnIndex || columns[columnIndex].FocusedRow != row;
        FocusedColumnIndex = columnIndex;
        columns[columnIndex].FocusedRow = row;
        return changed;
    }

    public bool FocusColumn(HorizontalDirection direction)
    {
        var target = FocusedColumnIndex + (direction == HorizontalDirection.Left ? -1 : 1);
        if (target < 0 || target >= columns.Count) return false;
        FocusedColumnIndex = target;
        return true;
    }

    public bool FocusFirstColumn()
    {
        if (IsEmpty || FocusedColumnIndex == 0) return false;
        FocusedColumnIndex = 0;
        return true;
    }

    public bool FocusLastColumn()
    {
        if (IsEmpty || FocusedColumnIndex == columns.Count - 1) return false;
        FocusedColumnIndex = columns.Count - 1;
        return true;
    }

    public bool FocusWindow(VerticalDirection direction)
    {
        if (IsEmpty) return false;
        var column = columns[FocusedColumnIndex];
        var target = column.FocusedRow + (direction == VerticalDirection.Up ? -1 : 1);
        if (target < 0 || target >= column.windowList.Count) return false;
        column.FocusedRow = target;
        return true;
    }

    // Moving

    public bool MoveColumn(HorizontalDirection direction)
    {
        var target = FocusedColumnIndex + (direction == HorizontalDirection.Left ? -1 : 1);
        if (IsEmpty || target < 0 || target >= columns.Count) return false;
        (columns[FocusedColumnIndex], columns[target]) = (columns[target], columns[FocusedColumnIndex]);
        FocusedColumnIndex = target;
        return true;
    }

    public bool MoveWindow(VerticalDirection direction)
    {
        if (IsEmpty) return false;
        var column = columns[FocusedColumnIndex];
        var windows = column.windowList;
        var target = column.FocusedRow + (direction == VerticalDirection.Up ? -1 : 1);
        if (target < 0 || target >= windows.Count) return false;
        (windows[column.FocusedRow], windows[target]) = (windows[target], windows[column.FocusedRow]);
        column.FocusedRow = target;
        return true;
    }

    /// <summary>
    /// niri's consume-or-expel: a window sharing its column is expelled into a new column on
    /// that side; a window alone in its column joins the bottom of the neighbouring column.
    /// </summary>
    public bool ConsumeOrExpel(HorizontalDirection direction)
    {
        if (IsEmpty) return false;
        var column = columns[FocusedColumnIndex];
        var window = column.FocusedWindow;

        if (column.windowList.Count > 1)
        {
            column.windowList.RemoveAt(column.FocusedRow);
            column.FocusedRow = Math.Min(column.FocusedRow, column.windowList.Count - 1);
            var target = direction == HorizontalDirection.Left ? FocusedColumnIndex : FocusedColumnIndex + 1;
            columns.Insert(target, new Column([window], widthIndex: column.WidthIndex));
            FocusedColumnIndex = target;
            return true;
        }

        var neighbour = FocusedColumnIndex + (direction == HorizontalDirection.Left ? -1 : 1);
        if (neighbour < 0 || neighbour >= columns.Count) return false;
        columns[neighbour].windowList.Add(window);
        columns[neighbour].FocusedRow = columns[neighbour].windowList.Count - 1;
        columns.RemoveAt(FocusedColumnIndex);
        FocusedColumnIndex = direction == HorizontalDirection.Left ? neighbour : neighbour - 1;
        return true;
    }

    // Width

    public bool CycleWidth(int presetCount, bool forward = true)
    {
        if (IsEmpty || presetCount <= 0) return false;
        var column = columns[FocusedColumnIndex];
        var step = forward ? 1 : presetCount - 1;
        column.WidthIndex = column.IsFullWidth ? column.WidthIndex : (column.WidthIndex + step) % presetCount;
        column.IsFullWidth = false;
        column.CustomWidth = null;
        return true;
    }

    /// <summary>Switches the focused column to a preset, dropping any custom or full width.</summary>
    public bool SetWidthIndex(int index)
    {
        if (IsEmpty) return false;
        var column = columns[FocusedColumnIndex];
        column.WidthIndex = index;
        column.IsFullWidth = false;
        column.CustomWidth = null;
        return true;
    }

    /// <summary>Gives the column holding <paramref name="window"/> a width in pixels, e.g. after the user resized it.</summary>
    public bool SetCustomWidth(double width, WindowId window)
    {
        if (Location(window) is not (int index, _)) return false;
        columns[index].CustomWidth = width;
        columns[index].IsFullWidth = false;
        return true;
    }

    public bool ToggleFullWidth()
    {
        if (IsEmpty) return false;
        columns[FocusedColumnIndex].IsFullWidth = !columns[FocusedColumnIndex].IsFullWidth;
        return true;
    }

    // Tabs

    /// <summary>Switches the focused column between stacked windows and tabs.</summary>
    public bool ToggleTabbed()
    {
        if (IsEmpty) return false;
        columns[FocusedColumnIndex].IsTabbed = !columns[FocusedColumnIndex].IsTabbed;
        return true;
    }

    public bool Equals(Strip? other) =>
        other is not null && FocusedColumnIndex == other.FocusedColumnIndex && columns.SequenceEqual(other.columns);

    public override bool Equals(object? obj) => Equals(obj as Strip);

    public override int GetHashCode() => HashCode.Combine(columns.Count, FocusedColumnIndex);

    public override string ToString() => string.Join(" ", columns);
}
