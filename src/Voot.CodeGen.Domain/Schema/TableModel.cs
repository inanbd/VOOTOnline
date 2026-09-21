namespace Voot.CodeGen.Domain.Schema;

/// <summary>
/// A database table. Replaces CodeSmith's <c>SchemaExplorer.TableSchema</c>.
/// </summary>
public sealed class TableModel
{
    public required string Name { get; init; }

    /// <summary>Schema/owner, e.g. <c>dbo</c>.</summary>
    public required string Owner { get; init; }

    public required IReadOnlyList<ColumnModel> Columns { get; init; }

    /// <summary>Null when the table has no primary key; such tables are skipped by the generator.</summary>
    public PrimaryKeyModel? PrimaryKey { get; init; }

    /// <summary>Foreign keys declared *on this table* (this table is the dependent side).</summary>
    public required IReadOnlyList<ForeignKeyModel> ForeignKeys { get; init; }

    public IReadOnlyList<IndexModel> Indexes { get; init; } = [];

    public bool HasPrimaryKey => PrimaryKey is not null;

    public IEnumerable<ColumnModel> NonPrimaryKeyColumns => Columns.Where(c => !c.IsPrimaryKeyMember);

    public IEnumerable<ColumnModel> ForeignKeyColumns => Columns.Where(c => c.IsForeignKeyMember);

    public ColumnModel? FindColumn(string name) =>
        Columns.FirstOrDefault(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));

    /// <summary>Fully qualified, bracket-quoted name for use in T-SQL.</summary>
    public string QualifiedName => $"[{Owner}].[{Name}]";

    public override string ToString() => QualifiedName;
}
