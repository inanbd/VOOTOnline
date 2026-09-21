namespace Voot.CodeGen.Domain.Schema;

/// <summary>Primary key definition. Replaces CodeSmith's <c>PrimaryKeySchema</c>.</summary>
public sealed class PrimaryKeyModel
{
    public required string Name { get; init; }

    /// <summary>Key columns in ordinal order. Never empty.</summary>
    public required IReadOnlyList<ColumnModel> MemberColumns { get; init; }

    public bool IsComposite => MemberColumns.Count > 1;

    public ColumnModel FirstColumn => MemberColumns[0];
}
