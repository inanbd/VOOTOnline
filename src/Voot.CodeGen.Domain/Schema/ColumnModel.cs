namespace Voot.CodeGen.Domain.Schema;

/// <summary>
/// A single column of a table. Replaces CodeSmith's <c>SchemaExplorer.ColumnSchema</c>.
/// </summary>
public sealed class ColumnModel
{
    public required string Name { get; init; }

    /// <summary>Zero-based position of the column within the table.</summary>
    public required int Ordinal { get; init; }

    /// <summary>The SQL Server native type, e.g. <c>nvarchar</c>.</summary>
    public required string NativeType { get; init; }

    public required SqlDataType DataType { get; init; }

    /// <summary>Character or byte length. <c>-1</c> represents MAX, <c>0</c> means not applicable.</summary>
    public int Size { get; init; }

    public int Precision { get; init; }

    public int Scale { get; init; }

    public bool AllowDbNull { get; init; }

    public bool IsIdentity { get; init; }

    public bool IsComputed { get; init; }

    public bool IsRowVersion { get; init; }

    public bool IsPrimaryKeyMember { get; init; }

    public bool IsForeignKeyMember { get; init; }

    public string? DefaultValue { get; init; }

    /// <summary>Name of the table this column belongs to; set by the schema reader.</summary>
    public string TableName { get; init; } = string.Empty;

    /// <summary>True when the type carries a meaningful length, so DDL and parameters need one.</summary>
    public bool HasLength => DataType is SqlDataType.Char or SqlDataType.NChar or SqlDataType.VarChar
        or SqlDataType.NVarChar or SqlDataType.Binary or SqlDataType.VarBinary;

    /// <summary>True when the type carries precision and scale.</summary>
    public bool HasPrecision => DataType is SqlDataType.Decimal;

    public bool IsMaxLength => Size == -1;

    public override string ToString() => $"{TableName}.{Name} ({NativeType})";
}
