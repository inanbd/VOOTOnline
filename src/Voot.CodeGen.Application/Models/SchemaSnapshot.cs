using Voot.CodeGen.Domain.Schema;

namespace Voot.CodeGen.Application.Models;

/// <summary>
/// The schema a successful run generated from, kept so a later run can work out which tables
/// changed since. A flat, serialisable shape rather than the domain model, which shares column
/// instances between tables, keys and foreign keys and exposes computed collections.
/// </summary>
public sealed record SchemaSnapshot
{
    public required IReadOnlyList<SnapshotTable> Tables { get; init; }

    /// <summary>
    /// Fingerprint of the generation settings the run used. When it differs, every generated
    /// file may differ, so a changed-tables run falls back to all tables.
    /// </summary>
    public required string SettingsFingerprint { get; init; }

    /// <summary>
    /// Tables the run could not generate. A later changed-tables run includes them again, so a
    /// table that failed once is not silently left out forever.
    /// </summary>
    public IReadOnlyList<string> FailedTables { get; init; } = [];

    public DateTimeOffset CapturedUtc { get; init; }

    public string DatabaseName { get; init; } = string.Empty;

    public static SchemaSnapshot From(
        DatabaseModel database, string settingsFingerprint, IEnumerable<string> failedTables, DateTimeOffset capturedUtc)
    {
        ArgumentNullException.ThrowIfNull(database);

        return new SchemaSnapshot
        {
            SettingsFingerprint = settingsFingerprint,
            FailedTables = [.. failedTables.Distinct(StringComparer.OrdinalIgnoreCase)],
            CapturedUtc = capturedUtc,
            DatabaseName = database.Name,
            Tables = [.. database.Tables.Select(t => new SnapshotTable
            {
                Owner = t.Owner,
                Name = t.Name,
                Columns = [.. t.Columns.Select(c => new SnapshotColumn
                {
                    Name = c.Name,
                    Ordinal = c.Ordinal,
                    NativeType = c.NativeType,
                    DataType = c.DataType,
                    Size = c.Size,
                    Precision = c.Precision,
                    Scale = c.Scale,
                    AllowDbNull = c.AllowDbNull,
                    IsIdentity = c.IsIdentity,
                    IsComputed = c.IsComputed,
                    IsRowVersion = c.IsRowVersion,
                    IsPrimaryKeyMember = c.IsPrimaryKeyMember,
                    IsForeignKeyMember = c.IsForeignKeyMember,
                    DefaultValue = c.DefaultValue
                })],
                PrimaryKeyName = t.PrimaryKey?.Name,
                PrimaryKeyColumns = [.. t.PrimaryKey?.MemberColumns.Select(c => c.Name) ?? []],
                ForeignKeys = [.. t.ForeignKeys.Select(f => new SnapshotForeignKey
                {
                    Name = f.Name,
                    Columns = [.. f.ForeignKeyMemberColumns.Select(c => c.Name)],
                    ReferencedTable = f.PrimaryKeyTableName,
                    ReferencedColumns = [.. f.PrimaryKeyMemberColumns.Select(c => c.Name)]
                })]
            })]
        };
    }

    /// <summary>Rebuilds a domain model good enough to compare against; indexes are not kept.</summary>
    public DatabaseModel ToDatabaseModel()
    {
        var tables = new List<TableModel>(Tables.Count);

        foreach (var t in Tables)
        {
            var columns = t.Columns.Select(c => new ColumnModel
            {
                Name = c.Name,
                Ordinal = c.Ordinal,
                NativeType = c.NativeType,
                DataType = c.DataType,
                Size = c.Size,
                Precision = c.Precision,
                Scale = c.Scale,
                AllowDbNull = c.AllowDbNull,
                IsIdentity = c.IsIdentity,
                IsComputed = c.IsComputed,
                IsRowVersion = c.IsRowVersion,
                IsPrimaryKeyMember = c.IsPrimaryKeyMember,
                IsForeignKeyMember = c.IsForeignKeyMember,
                DefaultValue = c.DefaultValue,
                TableName = t.Name
            }).ToList();

            ColumnModel? Find(string name) =>
                columns.FirstOrDefault(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));

            var keyColumns = t.PrimaryKeyColumns.Select(Find).OfType<ColumnModel>().ToList();

            tables.Add(new TableModel
            {
                Owner = t.Owner,
                Name = t.Name,
                Columns = columns,
                PrimaryKey = keyColumns.Count > 0
                    ? new PrimaryKeyModel { Name = t.PrimaryKeyName ?? string.Empty, MemberColumns = keyColumns }
                    : null,
                ForeignKeys = [.. t.ForeignKeys.Select(f => new ForeignKeyModel
                {
                    Name = f.Name,
                    ForeignKeyTableName = t.Name,
                    ForeignKeyMemberColumns = [.. f.Columns.Select(Find).OfType<ColumnModel>()],
                    PrimaryKeyTableName = f.ReferencedTable,
                    // The referenced columns only matter by name for comparison.
                    PrimaryKeyMemberColumns = [.. f.ReferencedColumns.Select(n => new ColumnModel
                    {
                        Name = n, Ordinal = 0, NativeType = string.Empty, DataType = SqlDataType.Unknown,
                        TableName = f.ReferencedTable
                    })]
                })]
            });
        }

        return new DatabaseModel { Name = DatabaseName, Tables = tables, ReadUtc = CapturedUtc };
    }
}

public sealed record SnapshotTable
{
    public required string Owner { get; init; }

    public required string Name { get; init; }

    public required IReadOnlyList<SnapshotColumn> Columns { get; init; }

    public string? PrimaryKeyName { get; init; }

    public IReadOnlyList<string> PrimaryKeyColumns { get; init; } = [];

    public IReadOnlyList<SnapshotForeignKey> ForeignKeys { get; init; } = [];
}

public sealed record SnapshotColumn
{
    public required string Name { get; init; }

    public int Ordinal { get; init; }

    public required string NativeType { get; init; }

    public SqlDataType DataType { get; init; }

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
}

public sealed record SnapshotForeignKey
{
    public required string Name { get; init; }

    public required IReadOnlyList<string> Columns { get; init; }

    public required string ReferencedTable { get; init; }

    public required IReadOnlyList<string> ReferencedColumns { get; init; }
}
