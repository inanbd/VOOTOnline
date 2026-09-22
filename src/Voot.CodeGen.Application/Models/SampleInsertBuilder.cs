using System.Globalization;
using System.Text;
using Voot.CodeGen.Domain.Schema;

namespace Voot.CodeGen.Application.Models;

/// <summary>
/// Builds a runnable <c>INSERT</c> with placeholder data for a table, so a column list does
/// not have to be typed out by hand to try something.
/// </summary>
public static class SampleInsertBuilder
{
    /// <summary>
    /// Builds the statement for one table. Identity, computed and rowversion columns are left
    /// out because SQL Server assigns them.
    /// </summary>
    public static string Build(TableModel table)
    {
        ArgumentNullException.ThrowIfNull(table);

        var columns = table.Columns
            .Where(c => !c.IsIdentity && !c.IsComputed && !c.IsRowVersion)
            .ToList();

        var builder = new StringBuilder();

        builder.Append("-- Sample INSERT for ").Append(table.QualifiedName).Append('\n');

        var skipped = table.Columns.Count - columns.Count;

        if (skipped > 0)
        {
            builder.Append("-- Omits ").Append(skipped)
                .Append(skipped == 1 ? " column" : " columns")
                .Append(" the server assigns (")
                .Append(string.Join(", ", table.Columns
                    .Where(c => c.IsIdentity || c.IsComputed || c.IsRowVersion)
                    .Select(c => c.Name)))
                .Append(").\n");
        }

        var requiredForeignKeys = columns
            .Where(c => c.IsForeignKeyMember && !c.AllowDbNull)
            .Select(c => c.Name)
            .ToList();

        if (requiredForeignKeys.Count > 0)
        {
            builder.Append("-- Replace ").Append(string.Join(", ", requiredForeignKeys))
                .Append(" with keys that exist in the referenced table(s).\n");
        }

        if (columns.Count == 0)
        {
            builder.Append("-- Every column is server-assigned; nothing to insert.\n");
            return builder.ToString();
        }

        builder.Append("INSERT INTO ").Append(table.QualifiedName).Append('\n').Append("(\n");
        builder.Append(string.Join(",\n", columns.Select(c => $"    [{c.Name}]"))).Append('\n');
        builder.Append(")\nVALUES\n(\n");
        builder.Append(string.Join(",\n", columns.Select(c => $"    {SampleValue(c)}"))).Append('\n');
        builder.Append(");\n");

        return builder.ToString();
    }

    /// <summary>Builds one script covering several tables, in the order given.</summary>
    public static string Build(IEnumerable<TableModel> tables)
    {
        ArgumentNullException.ThrowIfNull(tables);

        return string.Join("\n", tables.Select(Build)).TrimEnd('\n') + "\n";
    }

    /// <summary>
    /// A literal appropriate to the column's type. Nullable foreign keys become NULL so the
    /// statement runs as-is rather than failing on a constraint.
    /// </summary>
    public static string SampleValue(ColumnModel column)
    {
        ArgumentNullException.ThrowIfNull(column);

        if (column.IsForeignKeyMember && column.AllowDbNull)
        {
            return "NULL";
        }

        return column.NativeType.ToLowerInvariant() switch
        {
            "bit" => "1",
            "tinyint" => "1",
            "smallint" => "1",
            "int" => "1",
            "bigint" => "1",
            "decimal" or "numeric" => Numeric(column),
            "money" or "smallmoney" => "1.00",
            "float" or "real" => "1.0",
            "date" => "'2026-01-01'",
            "datetime" or "datetime2" or "smalldatetime" => "'2026-01-01 09:00:00'",
            "datetimeoffset" => "'2026-01-01 09:00:00 +00:00'",
            "time" => "'09:00:00'",
            "uniqueidentifier" => "NEWID()",
            "char" or "nchar" or "varchar" or "nvarchar" => Text(column),
            "text" or "ntext" => "N'Sample text'",
            "xml" => "N'<sample />'",
            "binary" or "varbinary" or "image" => "0x00",
            "sql_variant" => "N'Sample'",
            "sysname" => "N'Sample'",
            _ => "NULL"
        };
    }

    /// <summary>A string literal that respects the column's declared length.</summary>
    private static string Text(ColumnModel column)
    {
        var unicode = column.NativeType.StartsWith('n') ? "N" : string.Empty;
        var sample = "Sample";

        // A short column would reject the placeholder, so trim it to fit.
        if (column.Size > 0 && column.Size < sample.Length)
        {
            sample = sample[..column.Size];
        }

        return $"{unicode}'{sample.Replace("'", "''", StringComparison.Ordinal)}'";
    }

    /// <summary>A value that fits the column's scale, e.g. <c>1.00</c> for decimal(18, 2).</summary>
    private static string Numeric(ColumnModel column) =>
        column.Scale > 0
            ? (1m).ToString($"F{Math.Min(column.Scale, 10)}", CultureInfo.InvariantCulture)
            : "1";
}
