using Voot.CodeGen.Domain.Schema;

namespace Voot.CodeGen.Generation.Naming;

/// <summary>
/// How one SQL Server native type maps into generated C#: the CLR type name, the
/// <c>SqlDataReader</c> accessor, and the framework parameter helper to call.
/// </summary>
public sealed record SqlTypeInfo
{
    public required SqlDataType SqlType { get; init; }

    /// <summary>CLR type as the original templates wrote it, e.g. <c>Int64</c>.</summary>
    public required string LegacyClrType { get; init; }

    /// <summary>CLR type in modern C# keyword form, e.g. <c>long</c>.</summary>
    public required string ModernClrType { get; init; }

    /// <summary>
    /// True when the CLR type is a struct, so a nullable column needs <c>Nullable&lt;T&gt;</c>
    /// (Legacy) or <c>T?</c> (Modern). Reference types are already nullable.
    /// </summary>
    public required bool IsValueType { get; init; }

    /// <summary>
    /// Builds the expression that reads this column from a reader, given the ordinal
    /// expression (for example <c>start + 3</c>).
    /// </summary>
    public required Func<string, string> ReaderExpression { get; init; }

    /// <summary>Framework helper that builds the SqlParameter, e.g. <c>pNVarChar</c>.</summary>
    public required string ParameterHelper { get; init; }

    /// <summary>True when the helper takes a size argument between the name and the value.</summary>
    public bool ParameterTakesSize { get; init; }

    /// <summary>T-SQL type as written in a procedure's parameter list, e.g. <c>nvarchar(50)</c>.</summary>
    public required string SqlParameterType { get; init; }
}
