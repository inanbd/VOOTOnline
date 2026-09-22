namespace Voot.CodeGen.Domain.Schema;

/// <summary>
/// Compares two schema snapshots. Pure: it reads nothing and depends on nothing, so the
/// "what did my SQL do?" answer can be tested without a database.
/// </summary>
public static class SchemaComparer
{
    public static SchemaDiff Compare(DatabaseModel before, DatabaseModel after)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);

        var beforeTables = Index(before);
        var afterTables = Index(after);

        var added = after.Tables
            .Where(t => !beforeTables.ContainsKey(Key(t)))
            .ToList();

        var removed = before.Tables
            .Where(t => !afterTables.ContainsKey(Key(t)))
            .ToList();

        var changed = new List<TableDiff>();

        foreach (var (key, afterTable) in afterTables)
        {
            if (!beforeTables.TryGetValue(key, out var beforeTable))
            {
                continue;
            }

            var diff = CompareColumns(beforeTable, afterTable);

            if (diff.HasChanges)
            {
                changed.Add(diff);
            }
        }

        return new SchemaDiff(
            added,
            removed,
            [.. changed.OrderBy(t => t.TableName, StringComparer.OrdinalIgnoreCase)]);
    }

    private static TableDiff CompareColumns(TableModel before, TableModel after)
    {
        var beforeColumns = before.Columns.ToDictionary(c => c.Name, StringComparer.OrdinalIgnoreCase);
        var afterColumns = after.Columns.ToDictionary(c => c.Name, StringComparer.OrdinalIgnoreCase);

        var added = after.Columns
            .Where(c => !beforeColumns.ContainsKey(c.Name))
            .ToList();

        var removed = before.Columns
            .Where(c => !afterColumns.ContainsKey(c.Name))
            .ToList();

        var altered = new List<ColumnChange>();

        foreach (var afterColumn in after.Columns)
        {
            if (!beforeColumns.TryGetValue(afterColumn.Name, out var beforeColumn))
            {
                continue;
            }

            var from = Describe(beforeColumn);
            var to = Describe(afterColumn);

            if (!string.Equals(from, to, StringComparison.Ordinal))
            {
                altered.Add(new ColumnChange(afterColumn.Name, from, to));
            }
        }

        return new TableDiff(after.Name, added, removed, altered);
    }

    /// <summary>
    /// A column's declaration as a single comparable string. Two columns differ when this
    /// differs, which keeps "what changed" honest without enumerating every property.
    /// </summary>
    public static string Describe(ColumnModel column)
    {
        ArgumentNullException.ThrowIfNull(column);

        var type = column.NativeType;

        if (column.Size != 0 && IsSized(column.NativeType))
        {
            type += column.Size == -1 ? "(max)" : $"({column.Size})";
        }
        else if (IsPrecise(column.NativeType))
        {
            type += $"({column.Precision}, {column.Scale})";
        }

        var parts = new List<string> { type, column.AllowDbNull ? "NULL" : "NOT NULL" };

        if (column.IsIdentity)
        {
            parts.Add("identity");
        }

        if (column.IsComputed)
        {
            parts.Add("computed");
        }

        if (column.IsPrimaryKeyMember)
        {
            parts.Add("primary key");
        }

        if (!string.IsNullOrWhiteSpace(column.DefaultValue))
        {
            parts.Add($"default {column.DefaultValue}");
        }

        return string.Join(" ", parts);
    }

    private static bool IsSized(string nativeType) => nativeType.ToLowerInvariant()
        is "char" or "varchar" or "nchar" or "nvarchar" or "binary" or "varbinary";

    private static bool IsPrecise(string nativeType) => nativeType.ToLowerInvariant()
        is "decimal" or "numeric";

    private static Dictionary<string, TableModel> Index(DatabaseModel database) =>
        database.Tables.ToDictionary(Key, StringComparer.OrdinalIgnoreCase);

    /// <summary>Schema-qualified, so two tables of the same name in different schemas stay distinct.</summary>
    private static string Key(TableModel table) => $"{table.Owner}.{table.Name}";
}
