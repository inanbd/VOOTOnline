namespace Voot.CodeGen.Domain.Schema;

/// <summary>
/// A snapshot of a database's structure, read once per generation run.
/// Replaces CodeSmith's <c>SchemaExplorer.DatabaseSchema</c>.
/// </summary>
public sealed class DatabaseModel
{
    public required string Name { get; init; }

    public required IReadOnlyList<TableModel> Tables { get; init; }

    /// <summary>When the schema was read. Stored with the run so history is reproducible.</summary>
    public DateTimeOffset ReadUtc { get; init; } = DateTimeOffset.UtcNow;

    public TableModel? FindTable(string name) =>
        Tables.FirstOrDefault(t => string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase));

    public bool ContainsTable(string name) => FindTable(name) is not null;

    public IEnumerable<TableModel> TablesWithPrimaryKey => Tables.Where(t => t.HasPrimaryKey);

    public IEnumerable<TableModel> TablesWithoutPrimaryKey => Tables.Where(t => !t.HasPrimaryKey);
}
