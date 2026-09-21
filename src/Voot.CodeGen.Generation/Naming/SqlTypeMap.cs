using System.Globalization;
using Voot.CodeGen.Domain.Generation;
using Voot.CodeGen.Domain.Schema;

namespace Voot.CodeGen.Generation.Naming;

/// <summary>
/// Maps SQL Server native types onto CLR types, reader accessors and parameter helpers.
/// This is the port of the <c>GetCSharpVariableType</c>, <c>GetReaderValue</c>,
/// <c>GetSqlDbType</c> and <c>GetInParameterMethod</c> switches that were duplicated across
/// every original .cst template.
/// </summary>
public static class SqlTypeMap
{
    private static string Reader(string method) => method;

    private static readonly Dictionary<string, SqlTypeInfo> Map = new(StringComparer.OrdinalIgnoreCase)
    {
        ["bigint"] = new()
        {
            SqlType = SqlDataType.BigInt, LegacyClrType = "Int64", ModernClrType = "long", IsValueType = true,
            ReaderExpression = o => $"reader.GetInt64( {o} )", ParameterHelper = "pInt64", SqlParameterType = "bigint"
        },
        ["binary"] = new()
        {
            SqlType = SqlDataType.Binary, LegacyClrType = "Byte[]", ModernClrType = "byte[]", IsValueType = false,
            ReaderExpression = o => $"(Byte[])reader.GetValue( {o} )", ParameterHelper = "pBinary",
            ParameterTakesSize = true, SqlParameterType = "binary"
        },
        ["bit"] = new()
        {
            SqlType = SqlDataType.Bit, LegacyClrType = "Boolean", ModernClrType = "bool", IsValueType = true,
            ReaderExpression = o => $"reader.GetBoolean( {o} )", ParameterHelper = "pBool", SqlParameterType = "bit"
        },
        ["char"] = new()
        {
            SqlType = SqlDataType.Char, LegacyClrType = "Char", ModernClrType = "char", IsValueType = true,
            ReaderExpression = o => $"Convert.ToChar(reader.GetValue( {o} ))", ParameterHelper = "pChar",
            ParameterTakesSize = true, SqlParameterType = "char"
        },
        ["date"] = new()
        {
            SqlType = SqlDataType.Date, LegacyClrType = "DateTime", ModernClrType = "DateTime", IsValueType = true,
            ReaderExpression = o => $"reader.GetDateTime( {o} )", ParameterHelper = "pDateTime", SqlParameterType = "date"
        },
        ["datetime"] = new()
        {
            SqlType = SqlDataType.DateTime, LegacyClrType = "DateTime", ModernClrType = "DateTime", IsValueType = true,
            ReaderExpression = o => $"reader.GetDateTime( {o} )", ParameterHelper = "pDateTime", SqlParameterType = "datetime"
        },
        ["datetime2"] = new()
        {
            SqlType = SqlDataType.DateTime2, LegacyClrType = "DateTime", ModernClrType = "DateTime", IsValueType = true,
            ReaderExpression = o => $"reader.GetDateTime( {o} )", ParameterHelper = "pDateTime", SqlParameterType = "datetime2"
        },
        ["smalldatetime"] = new()
        {
            SqlType = SqlDataType.SmallDateTime, LegacyClrType = "DateTime", ModernClrType = "DateTime", IsValueType = true,
            ReaderExpression = o => $"reader.GetDateTime( {o} )", ParameterHelper = "pSmallDateTime",
            SqlParameterType = "smalldatetime"
        },
        ["datetimeoffset"] = new()
        {
            SqlType = SqlDataType.DateTimeOffset, LegacyClrType = "DateTimeOffset", ModernClrType = "DateTimeOffset",
            IsValueType = true, ReaderExpression = o => $"reader.GetDateTimeOffset( {o} )",
            ParameterHelper = "pDateTimeOffset", SqlParameterType = "datetimeoffset"
        },
        ["decimal"] = new()
        {
            SqlType = SqlDataType.Decimal, LegacyClrType = "Decimal", ModernClrType = "decimal", IsValueType = true,
            ReaderExpression = o => $"reader.GetDecimal( {o} )", ParameterHelper = "pDecimal",
            ParameterTakesSize = true, SqlParameterType = "decimal"
        },
        ["numeric"] = new()
        {
            SqlType = SqlDataType.Decimal, LegacyClrType = "Decimal", ModernClrType = "decimal", IsValueType = true,
            ReaderExpression = o => $"reader.GetDecimal( {o} )", ParameterHelper = "pDecimal",
            ParameterTakesSize = true, SqlParameterType = "numeric"
        },
        ["float"] = new()
        {
            SqlType = SqlDataType.Float, LegacyClrType = "Double", ModernClrType = "double", IsValueType = true,
            ReaderExpression = o => $"reader.GetDouble( {o} )", ParameterHelper = "pDouble", SqlParameterType = "float"
        },
        ["image"] = new()
        {
            SqlType = SqlDataType.Image, LegacyClrType = "Byte[]", ModernClrType = "byte[]", IsValueType = false,
            ReaderExpression = o => $"(Byte[])reader.GetValue( {o} )", ParameterHelper = "pImage", SqlParameterType = "image"
        },
        ["int"] = new()
        {
            SqlType = SqlDataType.Int, LegacyClrType = "Int32", ModernClrType = "int", IsValueType = true,
            ReaderExpression = o => $"reader.GetInt32( {o} )", ParameterHelper = "pInt32", SqlParameterType = "int"
        },
        ["money"] = new()
        {
            SqlType = SqlDataType.Money, LegacyClrType = "Decimal", ModernClrType = "decimal", IsValueType = true,
            ReaderExpression = o => $"reader.GetDecimal( {o} )", ParameterHelper = "pMoney", SqlParameterType = "money"
        },
        ["smallmoney"] = new()
        {
            SqlType = SqlDataType.SmallMoney, LegacyClrType = "Decimal", ModernClrType = "decimal", IsValueType = true,
            ReaderExpression = o => $"reader.GetDecimal( {o} )", ParameterHelper = "pDecimal", SqlParameterType = "smallmoney"
        },
        ["nchar"] = new()
        {
            SqlType = SqlDataType.NChar, LegacyClrType = "Char[]", ModernClrType = "char[]", IsValueType = false,
            ReaderExpression = o => $"reader.GetString( {o} ).ToCharArray()", ParameterHelper = "pNChar",
            ParameterTakesSize = true, SqlParameterType = "nchar"
        },
        ["ntext"] = new()
        {
            SqlType = SqlDataType.NText, LegacyClrType = "String", ModernClrType = "string", IsValueType = false,
            ReaderExpression = o => $"reader.GetString( {o} )", ParameterHelper = "pText", SqlParameterType = "ntext"
        },
        ["nvarchar"] = new()
        {
            SqlType = SqlDataType.NVarChar, LegacyClrType = "String", ModernClrType = "string", IsValueType = false,
            ReaderExpression = o => $"reader.GetString( {o} )", ParameterHelper = "pNVarChar",
            ParameterTakesSize = true, SqlParameterType = "nvarchar"
        },
        ["real"] = new()
        {
            SqlType = SqlDataType.Real, LegacyClrType = "Single", ModernClrType = "float", IsValueType = true,
            ReaderExpression = o => $"reader.GetFloat( {o} )", ParameterHelper = "pReal", SqlParameterType = "real"
        },
        ["smallint"] = new()
        {
            SqlType = SqlDataType.SmallInt, LegacyClrType = "Int16", ModernClrType = "short", IsValueType = true,
            ReaderExpression = o => $"reader.GetInt16( {o} )", ParameterHelper = "pInt16", SqlParameterType = "smallint"
        },
        ["sql_variant"] = new()
        {
            SqlType = SqlDataType.Variant, LegacyClrType = "Object", ModernClrType = "object", IsValueType = false,
            ReaderExpression = o => $"(Object)reader.GetValue( {o} )", ParameterHelper = "pVariant",
            SqlParameterType = "sql_variant"
        },
        ["sysname"] = new()
        {
            SqlType = SqlDataType.NVarChar, LegacyClrType = "String", ModernClrType = "string", IsValueType = false,
            ReaderExpression = o => $"reader.GetString( {o} )", ParameterHelper = "pNVarChar", SqlParameterType = "sysname"
        },
        ["text"] = new()
        {
            SqlType = SqlDataType.Text, LegacyClrType = "String", ModernClrType = "string", IsValueType = false,
            ReaderExpression = o => $"reader.GetString( {o} )", ParameterHelper = "pText", SqlParameterType = "text"
        },
        ["time"] = new()
        {
            SqlType = SqlDataType.Time, LegacyClrType = "TimeSpan", ModernClrType = "TimeSpan", IsValueType = true,
            ReaderExpression = o => $"reader.GetTimeSpan( {o} )", ParameterHelper = "pTime", SqlParameterType = "time"
        },
        ["timestamp"] = new()
        {
            SqlType = SqlDataType.Timestamp, LegacyClrType = "Byte[]", ModernClrType = "byte[]", IsValueType = false,
            ReaderExpression = o => $"(Byte[])reader.GetValue( {o} )", ParameterHelper = "pTimestamp",
            SqlParameterType = "timestamp"
        },
        ["rowversion"] = new()
        {
            SqlType = SqlDataType.Timestamp, LegacyClrType = "Byte[]", ModernClrType = "byte[]", IsValueType = false,
            ReaderExpression = o => $"(Byte[])reader.GetValue( {o} )", ParameterHelper = "pTimestamp",
            SqlParameterType = "rowversion"
        },
        ["tinyint"] = new()
        {
            SqlType = SqlDataType.TinyInt, LegacyClrType = "Byte", ModernClrType = "byte", IsValueType = true,
            ReaderExpression = o => $"reader.GetByte( {o} )", ParameterHelper = "pByte", SqlParameterType = "tinyint"
        },
        ["uniqueidentifier"] = new()
        {
            SqlType = SqlDataType.UniqueIdentifier, LegacyClrType = "Guid", ModernClrType = "Guid", IsValueType = true,
            ReaderExpression = o => $"reader.GetGuid( {o} )", ParameterHelper = "pGuid",
            SqlParameterType = "uniqueidentifier"
        },
        ["varbinary"] = new()
        {
            SqlType = SqlDataType.VarBinary, LegacyClrType = "Byte[]", ModernClrType = "byte[]", IsValueType = false,
            ReaderExpression = o => $"(Byte[])reader.GetValue( {o} )", ParameterHelper = "pVarBinary",
            ParameterTakesSize = true, SqlParameterType = "varbinary"
        },
        ["varchar"] = new()
        {
            SqlType = SqlDataType.VarChar, LegacyClrType = "String", ModernClrType = "string", IsValueType = false,
            ReaderExpression = o => $"reader.GetString( {o} )", ParameterHelper = "pVarChar",
            ParameterTakesSize = true, SqlParameterType = "varchar"
        },
        ["xml"] = new()
        {
            SqlType = SqlDataType.Xml, LegacyClrType = "String", ModernClrType = "string", IsValueType = false,
            ReaderExpression = o => $"reader.GetString( {o} )", ParameterHelper = "pXml", SqlParameterType = "xml"
        }
    };

    /// <summary>
    /// True when a key column is numeric, so a "maximum key" procedure can default to zero.
    /// A uniqueidentifier or string key has no meaningful maximum, and ISNULL(MAX(x), 0)
    /// against one is an operand type clash, so the procedure is skipped for those.
    /// </summary>
    public static bool SupportsMaximumKey(ColumnModel column) =>
        TryGet(column.NativeType) is { SqlType: SqlDataType.BigInt or SqlDataType.Int or SqlDataType.SmallInt
            or SqlDataType.TinyInt or SqlDataType.Decimal or SqlDataType.Money or SqlDataType.SmallMoney };

    /// <summary>True when the native type has a mapping; unknown types raise a diagnostic.</summary>
    public static bool IsKnown(string nativeType) => Map.ContainsKey(nativeType);

    public static SqlTypeInfo? TryGet(string nativeType) =>
        Map.TryGetValue(nativeType, out var info) ? info : null;

    /// <summary>Resolves a column's type mapping, throwing if the native type is unsupported.</summary>
    public static SqlTypeInfo Require(ColumnModel column) =>
        TryGet(column.NativeType)
        ?? throw new NotSupportedException(
            $"Column {column.TableName}.{column.Name} uses unsupported SQL type '{column.NativeType}'.");

    /// <summary>
    /// The C# type for a column, honouring nullability. Legacy writes <c>Nullable&lt;Int32&gt;</c>
    /// and framework type names; Modern writes <c>int?</c> and language keywords.
    /// </summary>
    public static string ClrType(ColumnModel column, OutputStyle style)
    {
        var info = Require(column);
        var baseType = style == OutputStyle.Legacy ? info.LegacyClrType : info.ModernClrType;

        if (!column.AllowDbNull)
        {
            return baseType;
        }

        // Reference types are already nullable; only structs get wrapped.
        if (!info.IsValueType)
        {
            return style == OutputStyle.Modern ? baseType + "?" : baseType;
        }

        return style == OutputStyle.Legacy ? $"Nullable<{baseType}>" : baseType + "?";
    }

    /// <summary>The expression that reads this column at <paramref name="ordinalExpression"/>.</summary>
    public static string ReaderValue(ColumnModel column, string ordinalExpression) =>
        Require(column).ReaderExpression(ordinalExpression);

    /// <summary>
    /// The T-SQL type for a procedure parameter, including length or precision,
    /// e.g. <c>nvarchar(50)</c>, <c>nvarchar(max)</c> or <c>decimal(18, 2)</c>.
    /// </summary>
    public static string SqlParameterType(ColumnModel column)
    {
        var info = Require(column);

        if (column.HasLength)
        {
            // Emitted SQL must not depend on the server's locale.
            var length = column.IsMaxLength ? "max" : column.Size.ToString(CultureInfo.InvariantCulture);
            return $"{info.SqlParameterType}({length})";
        }

        if (column.HasPrecision)
        {
            return string.Create(CultureInfo.InvariantCulture,
                $"{info.SqlParameterType}({column.Precision}, {column.Scale})");
        }

        return info.SqlParameterType;
    }
}
