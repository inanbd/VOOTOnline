namespace Voot.CodeGen.Domain.Schema;

/// <summary>
/// A foreign key relationship. Replaces CodeSmith's <c>TableKeySchema</c>.
/// </summary>
public sealed class ForeignKeyModel
{
    public required string Name { get; init; }

    /// <summary>The table holding the foreign key columns (the dependent/child table).</summary>
    public required string ForeignKeyTableName { get; init; }

    /// <summary>Columns on the child table that make up the key.</summary>
    public required IReadOnlyList<ColumnModel> ForeignKeyMemberColumns { get; init; }

    /// <summary>The referenced table (the principal/parent table).</summary>
    public required string PrimaryKeyTableName { get; init; }

    /// <summary>Columns on the parent table being referenced.</summary>
    public required IReadOnlyList<ColumnModel> PrimaryKeyMemberColumns { get; init; }

    public override string ToString() =>
        $"{ForeignKeyTableName}({string.Join(',', ForeignKeyMemberColumns.Select(c => c.Name))}) -> {PrimaryKeyTableName}";
}
