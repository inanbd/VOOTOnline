namespace Voot.CodeGen.Domain.Schema;

/// <summary>A table index, used by the optional select/delete-by-index procedures.</summary>
public sealed class IndexModel
{
    public required string Name { get; init; }

    public required IReadOnlyList<ColumnModel> MemberColumns { get; init; }

    public bool IsUnique { get; init; }

    public bool IsPrimaryKey { get; init; }

    public bool IsClustered { get; init; }
}
