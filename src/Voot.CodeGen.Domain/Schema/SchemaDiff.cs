namespace Voot.CodeGen.Domain.Schema;

/// <summary>How one column differs between two snapshots.</summary>
/// <param name="ColumnName">The column's name.</param>
/// <param name="Before">Its declaration before the change.</param>
/// <param name="After">Its declaration after the change.</param>
public sealed record ColumnChange(string ColumnName, string Before, string After);

/// <summary>What changed inside one table that exists in both snapshots.</summary>
public sealed record TableDiff(
    string TableName,
    IReadOnlyList<ColumnModel> AddedColumns,
    IReadOnlyList<ColumnModel> RemovedColumns,
    IReadOnlyList<ColumnChange> ChangedColumns)
{
    public bool HasChanges =>
        AddedColumns.Count > 0 || RemovedColumns.Count > 0 || ChangedColumns.Count > 0;
}

/// <summary>
/// The structural difference between two schema snapshots, used to show what a submitted SQL
/// change actually did to the database.
/// </summary>
public sealed record SchemaDiff(
    IReadOnlyList<TableModel> AddedTables,
    IReadOnlyList<TableModel> RemovedTables,
    IReadOnlyList<TableDiff> ChangedTables)
{
    public static readonly SchemaDiff Empty = new([], [], []);

    public bool HasChanges =>
        AddedTables.Count > 0 || RemovedTables.Count > 0 || ChangedTables.Count > 0;

    public int AddedColumnCount => ChangedTables.Sum(t => t.AddedColumns.Count);

    public int RemovedColumnCount => ChangedTables.Sum(t => t.RemovedColumns.Count);

    public int AlteredColumnCount => ChangedTables.Sum(t => t.ChangedColumns.Count);

    /// <summary>
    /// A one-line description such as <c>+1 table, +2 columns, 1 column altered</c>, stored
    /// against the change so the history says what happened without a second snapshot.
    /// </summary>
    public string Summary()
    {
        if (!HasChanges)
        {
            return "no structural change";
        }

        var parts = new List<string>();

        Add(parts, AddedTables.Count, "+{0} table{1}");
        Add(parts, RemovedTables.Count, "-{0} table{1}");
        Add(parts, AddedColumnCount, "+{0} column{1}");
        Add(parts, RemovedColumnCount, "-{0} column{1}");
        Add(parts, AlteredColumnCount, "{0} column{1} altered");

        return string.Join(", ", parts);
    }

    private static void Add(List<string> parts, int count, string format)
    {
        if (count > 0)
        {
            parts.Add(string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                format, count, count == 1 ? string.Empty : "s"));
        }
    }
}
