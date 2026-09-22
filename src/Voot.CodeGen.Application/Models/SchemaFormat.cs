using Voot.CodeGen.Domain.Schema;

namespace Voot.CodeGen.Application.Models;

/// <summary>
/// Describes schema objects the way SQL Server declares them. Deliberately separate from
/// <c>SqlTypeMap</c>, which throws on a type the generator cannot map: the schema browser has
/// to be able to show whatever is actually in the database.
/// </summary>
public static class SchemaFormat
{
    /// <summary>Native types that carry a length, e.g. <c>nvarchar(50)</c>.</summary>
    private static readonly HashSet<string> LengthTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "char", "varchar", "nchar", "nvarchar", "binary", "varbinary"
    };

    /// <summary>Native types that carry precision and scale.</summary>
    private static readonly HashSet<string> PrecisionTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "decimal", "numeric"
    };

    /// <summary>
    /// A column's type as it would be declared, e.g. <c>nvarchar(50)</c>, <c>nvarchar(max)</c>
    /// or <c>decimal(18, 2)</c>.
    /// </summary>
    /// <remarks>
    /// Keyed off the native type name rather than the generator's mapped enum, so a type the
    /// generator does not understand is still described accurately.
    /// </remarks>
    public static string SqlType(ColumnModel column)
    {
        ArgumentNullException.ThrowIfNull(column);

        var native = column.NativeType;

        if (LengthTypes.Contains(native))
        {
            return column.Size == -1 ? $"{native}(max)" : $"{native}({column.Size})";
        }

        return PrecisionTypes.Contains(native)
            ? $"{native}({column.Precision}, {column.Scale})"
            : native;
    }

    /// <summary>
    /// A column's default constraint with SQL Server's wrapping parentheses removed, or null
    /// when it has none. The catalog stores a literal as <c>((0))</c> and a call as
    /// <c>(getutcdate())</c>, neither of which is worth showing verbatim.
    /// </summary>
    public static string? Default(ColumnModel column)
    {
        ArgumentNullException.ThrowIfNull(column);

        var value = column.DefaultValue?.Trim();

        if (string.IsNullOrEmpty(value))
        {
            return null;
        }

        while (value.Length > 2 && value[0] == '(' && value[^1] == ')')
        {
            var inner = value[1..^1];

            // Only unwrap when the outer pair really did enclose the whole value; without this
            // "(a) + (b)" would lose its first and last characters.
            if (!IsBalanced(inner))
            {
                break;
            }

            value = inner.Trim();
        }

        return value.Length == 0 ? null : value;
    }

    private static bool IsBalanced(string value)
    {
        var depth = 0;

        foreach (var c in value)
        {
            if (c == '(')
            {
                depth++;
            }
            else if (c == ')' && --depth < 0)
            {
                return false;
            }
        }

        return depth == 0;
    }
}
