using Dapper;
using Microsoft.Data.SqlClient;
using Voot.CodeGen.Application.Abstractions;
using Voot.CodeGen.Application.Models;
using Voot.CodeGen.Domain.Schema;

namespace Voot.CodeGen.Infrastructure.Schema;

/// <summary>
/// Reads table, column, key and index metadata from SQL Server's catalog views into the
/// generator's schema model. This is the replacement for CodeSmith's SchemaExplorer provider.
/// </summary>
public sealed class SqlServerSchemaReader : ISchemaReader
{
    public async Task<string?> TestConnectionAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);
            await connection.ExecuteScalarAsync<int>(new CommandDefinition("SELECT 1", cancellationToken: cancellationToken));
            return null;
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }

    public async Task<IReadOnlyList<TableSummary>> ListTablesAsync(
        string connectionString, CancellationToken cancellationToken = default)
    {
        // One pass over the catalog: enough for the picker, without the column, key and index
        // reads that a full ReadAsync does.
        const string sql = """
            SELECT
                s.name AS Owner,
                t.name AS Name,
                (SELECT COUNT(*) FROM sys.columns c WHERE c.object_id = t.object_id) AS ColumnCount,
                CAST(CASE WHEN EXISTS (
                    SELECT 1 FROM sys.key_constraints kc
                    WHERE kc.parent_object_id = t.object_id AND kc.type = 'PK'
                ) THEN 1 ELSE 0 END AS bit) AS HasPrimaryKey
            FROM sys.tables t
            INNER JOIN sys.schemas s ON s.schema_id = t.schema_id
            WHERE t.is_ms_shipped = 0
            ORDER BY s.name, t.name;
            """;

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        var rows = await connection.QueryAsync<TableSummaryRow>(
            new CommandDefinition(sql, cancellationToken: cancellationToken));

        return [.. rows.Select(r => new TableSummary(r.Owner, r.Name, r.ColumnCount, r.HasPrimaryKey))];
    }

    /// <summary>Dapper maps by column name, which rules out projecting straight into the record struct.</summary>
    private sealed record TableSummaryRow(string Owner, string Name, int ColumnCount, bool HasPrimaryKey);

    public async Task<DatabaseModel> ReadAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        var columns = await ReadColumnsAsync(connection, cancellationToken);
        var primaryKeys = await ReadPrimaryKeysAsync(connection, cancellationToken);
        var foreignKeys = await ReadForeignKeysAsync(connection, cancellationToken);
        var indexes = await ReadIndexesAsync(connection, cancellationToken);

        var tables = BuildTables(columns, primaryKeys, foreignKeys, indexes);

        return new DatabaseModel
        {
            Name = connection.Database,
            Tables = tables
        };
    }

    // ---- raw rows -------------------------------------------------------------------

    private sealed record ColumnRow(
        string SchemaName, string TableName, string ColumnName, int Ordinal, string NativeType,
        int MaxLength, byte Precision, byte Scale, bool IsNullable, bool IsIdentity, bool IsComputed,
        string? DefaultValue);

    private sealed record KeyRow(string SchemaName, string TableName, string ConstraintName, string ColumnName, int Ordinal);

    private sealed record ForeignKeyRow(
        string ConstraintName, string SchemaName, string TableName, string ColumnName,
        string ReferencedSchema, string ReferencedTable, string ReferencedColumn, int Ordinal);

    private sealed record IndexRow(
        string SchemaName, string TableName, string IndexName, string ColumnName, int Ordinal,
        bool IsUnique, bool IsPrimaryKey, bool IsClustered);

    // ---- queries --------------------------------------------------------------------

    private static async Task<List<ColumnRow>> ReadColumnsAsync(SqlConnection connection, CancellationToken ct)
    {
        // max_length is in bytes; nchar/nvarchar report double their character length, and -1 means MAX.
        const string sql = """
            SELECT
                s.name                                        AS SchemaName,
                t.name                                        AS TableName,
                c.name                                        AS ColumnName,
                c.column_id - 1                               AS Ordinal,
                ty.name                                       AS NativeType,
                CASE
                    WHEN c.max_length = -1 THEN -1
                    WHEN ty.name IN ('nchar', 'nvarchar') THEN c.max_length / 2
                    ELSE c.max_length
                END                                           AS MaxLength,
                c.precision                                   AS Precision,
                c.scale                                       AS Scale,
                c.is_nullable                                 AS IsNullable,
                c.is_identity                                 AS IsIdentity,
                c.is_computed                                 AS IsComputed,
                dc.definition                                 AS DefaultValue
            FROM sys.columns c
            INNER JOIN sys.tables t      ON t.object_id = c.object_id
            INNER JOIN sys.schemas s     ON s.schema_id = t.schema_id
            INNER JOIN sys.types ty      ON ty.user_type_id = c.user_type_id
            LEFT JOIN sys.default_constraints dc ON dc.object_id = c.default_object_id
            WHERE t.is_ms_shipped = 0
            ORDER BY s.name, t.name, c.column_id;
            """;

        return [.. await connection.QueryAsync<ColumnRow>(new CommandDefinition(sql, cancellationToken: ct))];
    }

    private static async Task<List<KeyRow>> ReadPrimaryKeysAsync(SqlConnection connection, CancellationToken ct)
    {
        const string sql = """
            SELECT
                s.name  AS SchemaName,
                t.name  AS TableName,
                kc.name AS ConstraintName,
                c.name  AS ColumnName,
                CAST(ic.key_ordinal AS int) AS Ordinal
            FROM sys.key_constraints kc
            INNER JOIN sys.tables t        ON t.object_id = kc.parent_object_id
            INNER JOIN sys.schemas s       ON s.schema_id = t.schema_id
            INNER JOIN sys.index_columns ic ON ic.object_id = kc.parent_object_id AND ic.index_id = kc.unique_index_id
            INNER JOIN sys.columns c       ON c.object_id = ic.object_id AND c.column_id = ic.column_id
            WHERE kc.type = 'PK' AND t.is_ms_shipped = 0
            ORDER BY s.name, t.name, ic.key_ordinal;
            """;

        return [.. await connection.QueryAsync<KeyRow>(new CommandDefinition(sql, cancellationToken: ct))];
    }

    private static async Task<List<ForeignKeyRow>> ReadForeignKeysAsync(SqlConnection connection, CancellationToken ct)
    {
        const string sql = """
            SELECT
                fk.name        AS ConstraintName,
                s.name         AS SchemaName,
                t.name         AS TableName,
                c.name         AS ColumnName,
                rs.name        AS ReferencedSchema,
                rt.name        AS ReferencedTable,
                rc.name        AS ReferencedColumn,
                CAST(fkc.constraint_column_id AS int) AS Ordinal
            FROM sys.foreign_keys fk
            INNER JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id = fk.object_id
            INNER JOIN sys.tables t    ON t.object_id = fk.parent_object_id
            INNER JOIN sys.schemas s   ON s.schema_id = t.schema_id
            INNER JOIN sys.columns c   ON c.object_id = fkc.parent_object_id AND c.column_id = fkc.parent_column_id
            INNER JOIN sys.tables rt   ON rt.object_id = fk.referenced_object_id
            INNER JOIN sys.schemas rs  ON rs.schema_id = rt.schema_id
            INNER JOIN sys.columns rc  ON rc.object_id = fkc.referenced_object_id AND rc.column_id = fkc.referenced_column_id
            WHERE t.is_ms_shipped = 0
            ORDER BY fk.name, fkc.constraint_column_id;
            """;

        return [.. await connection.QueryAsync<ForeignKeyRow>(new CommandDefinition(sql, cancellationToken: ct))];
    }

    private static async Task<List<IndexRow>> ReadIndexesAsync(SqlConnection connection, CancellationToken ct)
    {
        const string sql = """
            SELECT
                s.name  AS SchemaName,
                t.name  AS TableName,
                i.name  AS IndexName,
                c.name  AS ColumnName,
                CAST(ic.key_ordinal AS int) AS Ordinal,
                i.is_unique       AS IsUnique,
                i.is_primary_key  AS IsPrimaryKey,
                CAST(CASE WHEN i.type = 1 THEN 1 ELSE 0 END AS bit) AS IsClustered
            FROM sys.indexes i
            INNER JOIN sys.tables t         ON t.object_id = i.object_id
            INNER JOIN sys.schemas s        ON s.schema_id = t.schema_id
            INNER JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
            INNER JOIN sys.columns c        ON c.object_id = ic.object_id AND c.column_id = ic.column_id
            WHERE t.is_ms_shipped = 0 AND i.name IS NOT NULL AND ic.is_included_column = 0
            ORDER BY s.name, t.name, i.name, ic.key_ordinal;
            """;

        return [.. await connection.QueryAsync<IndexRow>(new CommandDefinition(sql, cancellationToken: ct))];
    }

    // ---- assembly -------------------------------------------------------------------

    private static List<TableModel> BuildTables(
        List<ColumnRow> columnRows,
        List<KeyRow> keyRows,
        List<ForeignKeyRow> foreignKeyRows,
        List<IndexRow> indexRows)
    {
        var primaryKeyColumns = keyRows
            .GroupBy(k => (k.SchemaName, k.TableName))
            .ToDictionary(g => g.Key, g => g.OrderBy(k => k.Ordinal).ToList());

        var foreignKeyColumnNames = foreignKeyRows
            .GroupBy(f => (f.SchemaName, f.TableName))
            .ToDictionary(
                g => g.Key,
                g => g.Select(f => f.ColumnName).ToHashSet(StringComparer.OrdinalIgnoreCase));

        // Columns first: every later structure references these instances.
        var columnsByTable = new Dictionary<(string, string), List<ColumnModel>>();

        foreach (var group in columnRows.GroupBy(c => (c.SchemaName, c.TableName)))
        {
            var key = group.Key;
            var keyNames = primaryKeyColumns.TryGetValue(key, out var pk)
                ? pk.Select(k => k.ColumnName).ToHashSet(StringComparer.OrdinalIgnoreCase)
                : [];
            var fkNames = foreignKeyColumnNames.TryGetValue(key, out var fk) ? fk : [];

            columnsByTable[key] = [.. group
                .OrderBy(c => c.Ordinal)
                .Select(c => new ColumnModel
                {
                    Name = c.ColumnName,
                    Ordinal = c.Ordinal,
                    NativeType = c.NativeType,
                    DataType = MapType(c.NativeType),
                    Size = c.MaxLength,
                    Precision = c.Precision,
                    Scale = c.Scale,
                    AllowDbNull = c.IsNullable,
                    IsIdentity = c.IsIdentity,
                    IsComputed = c.IsComputed,
                    IsRowVersion = c.NativeType is "timestamp" or "rowversion",
                    IsPrimaryKeyMember = keyNames.Contains(c.ColumnName),
                    IsForeignKeyMember = fkNames.Contains(c.ColumnName),
                    DefaultValue = c.DefaultValue,
                    TableName = c.TableName
                })];
        }

        var tables = new List<TableModel>();

        foreach (var (key, columns) in columnsByTable.OrderBy(k => k.Key.Item1).ThenBy(k => k.Key.Item2))
        {
            var (schemaName, tableName) = key;

            PrimaryKeyModel? primaryKey = null;

            if (primaryKeyColumns.TryGetValue(key, out var pkRows) && pkRows.Count > 0)
            {
                var members = pkRows
                    .Select(r => columns.FirstOrDefault(c =>
                        string.Equals(c.Name, r.ColumnName, StringComparison.OrdinalIgnoreCase)))
                    .Where(c => c is not null)
                    .Select(c => c!)
                    .ToList();

                if (members.Count > 0)
                {
                    primaryKey = new PrimaryKeyModel { Name = pkRows[0].ConstraintName, MemberColumns = members };
                }
            }

            var foreignKeys = foreignKeyRows
                .Where(f => f.SchemaName == schemaName && f.TableName == tableName)
                .GroupBy(f => f.ConstraintName)
                .Select(g =>
                {
                    var ordered = g.OrderBy(f => f.Ordinal).ToList();
                    var referencedTable = ordered[0].ReferencedTable;

                    return new ForeignKeyModel
                    {
                        Name = g.Key,
                        ForeignKeyTableName = tableName,
                        ForeignKeyMemberColumns = [.. ordered
                            .Select(f => columns.FirstOrDefault(c =>
                                string.Equals(c.Name, f.ColumnName, StringComparison.OrdinalIgnoreCase)))
                            .Where(c => c is not null)
                            .Select(c => c!)],
                        PrimaryKeyTableName = referencedTable,
                        PrimaryKeyMemberColumns = [.. ordered
                            .Select(f => ResolveReferencedColumn(columnsByTable, f))
                            .Where(c => c is not null)
                            .Select(c => c!)]
                    };
                })
                .Where(f => f.ForeignKeyMemberColumns.Count > 0)
                .ToList();

            var indexes = indexRows
                .Where(i => i.SchemaName == schemaName && i.TableName == tableName)
                .GroupBy(i => i.IndexName)
                .Select(g => new IndexModel
                {
                    Name = g.Key,
                    IsUnique = g.First().IsUnique,
                    IsPrimaryKey = g.First().IsPrimaryKey,
                    IsClustered = g.First().IsClustered,
                    MemberColumns = [.. g
                        .OrderBy(i => i.Ordinal)
                        .Select(i => columns.FirstOrDefault(c =>
                            string.Equals(c.Name, i.ColumnName, StringComparison.OrdinalIgnoreCase)))
                        .Where(c => c is not null)
                        .Select(c => c!)]
                })
                .ToList();

            tables.Add(new TableModel
            {
                Name = tableName,
                Owner = schemaName,
                Columns = columns,
                PrimaryKey = primaryKey,
                ForeignKeys = foreignKeys,
                Indexes = indexes
            });
        }

        return tables;
    }

    private static ColumnModel? ResolveReferencedColumn(
        Dictionary<(string, string), List<ColumnModel>> columnsByTable, ForeignKeyRow row) =>
        columnsByTable.TryGetValue((row.ReferencedSchema, row.ReferencedTable), out var referenced)
            ? referenced.FirstOrDefault(c => string.Equals(c.Name, row.ReferencedColumn, StringComparison.OrdinalIgnoreCase))
            : null;

    private static SqlDataType MapType(string nativeType) => nativeType.ToLowerInvariant() switch
    {
        "bigint" => SqlDataType.BigInt,
        "binary" => SqlDataType.Binary,
        "bit" => SqlDataType.Bit,
        "char" => SqlDataType.Char,
        "date" => SqlDataType.Date,
        "datetime" => SqlDataType.DateTime,
        "datetime2" => SqlDataType.DateTime2,
        "datetimeoffset" => SqlDataType.DateTimeOffset,
        "decimal" or "numeric" => SqlDataType.Decimal,
        "float" => SqlDataType.Float,
        "image" => SqlDataType.Image,
        "int" => SqlDataType.Int,
        "money" => SqlDataType.Money,
        "nchar" => SqlDataType.NChar,
        "ntext" => SqlDataType.NText,
        "nvarchar" or "sysname" => SqlDataType.NVarChar,
        "real" => SqlDataType.Real,
        "smalldatetime" => SqlDataType.SmallDateTime,
        "smallint" => SqlDataType.SmallInt,
        "smallmoney" => SqlDataType.SmallMoney,
        "sql_variant" => SqlDataType.Variant,
        "text" => SqlDataType.Text,
        "time" => SqlDataType.Time,
        "timestamp" or "rowversion" => SqlDataType.Timestamp,
        "tinyint" => SqlDataType.TinyInt,
        "uniqueidentifier" => SqlDataType.UniqueIdentifier,
        "varbinary" => SqlDataType.VarBinary,
        "varchar" => SqlDataType.VarChar,
        "xml" => SqlDataType.Xml,
        _ => SqlDataType.Unknown
    };
}
