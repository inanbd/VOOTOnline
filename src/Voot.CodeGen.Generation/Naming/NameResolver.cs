using Voot.CodeGen.Domain.Generation;
using Voot.CodeGen.Domain.Projects;
using Voot.CodeGen.Domain.Schema;

namespace Voot.CodeGen.Generation.Naming;

/// <summary>
/// Derives every identifier the generator needs from a table or column. This consolidates the
/// <c>GetClassName</c> / <c>GetPropertyName</c> / <c>Get*ProcedureName</c> helpers that the
/// original templates each redefined, so class names and procedure names can no longer drift
/// apart between the data access layer and the T-SQL scripts.
/// </summary>
public sealed class NameResolver(GenerationSettings settings)
{
    private readonly GenerationSettings _settings = settings;

    /// <summary>Table name with the configured prefix removed, e.g. <c>tbl_User</c> -&gt; <c>User</c>.</summary>
    public string StripPrefix(string name) =>
        _settings.TablePrefix.Length > 0 && name.StartsWith(_settings.TablePrefix, StringComparison.OrdinalIgnoreCase)
            ? name[_settings.TablePrefix.Length..]
            : name;

    public string ClassName(TableModel table) => StripPrefix(table.Name);

    public string CamelCaseClassName(TableModel table) => ToCamelCase(ClassName(table));

    public string UpperCaseClassName(TableModel table) => ClassName(table).ToUpperInvariant();

    /// <summary>Property name for a column. The original templates used the column name verbatim.</summary>
    public static string PropertyName(ColumnModel column) => column.Name;

    public static string ToCamelCase(string value) =>
        value.Length == 0 ? value : char.ToLowerInvariant(value[0]) + value[1..];

    public static string ToPascalCase(string value) =>
        value.Length == 0 ? value : char.ToUpperInvariant(value[0]) + value[1..];

    public static string ToUpperCase(string value) => value.ToUpperInvariant();

    // ---- Primary key ------------------------------------------------------------------

    /// <summary>Name of the first primary key column; the templates assume a single-column key.</summary>
    public static string PrimaryKeyName(TableModel table) =>
        table.PrimaryKey?.FirstColumn.Name
        ?? throw new InvalidOperationException($"Table {table.Name} has no primary key.");

    public static ColumnModel PrimaryKeyColumn(TableModel table) =>
        table.PrimaryKey?.FirstColumn
        ?? throw new InvalidOperationException($"Table {table.Name} has no primary key.");

    public static string PrimaryKeyClrType(TableModel table, OutputStyle style)
    {
        var column = PrimaryKeyColumn(table);

        // A key column is never null, so map it as non-nullable even if the schema allows it.
        var info = SqlTypeMap.Require(column);
        return style == OutputStyle.Legacy ? info.LegacyClrType : info.ModernClrType;
    }

    // ---- Foreign keys -----------------------------------------------------------------

    /// <summary>
    /// Property-name stem for a foreign key: the concatenated names of its member columns,
    /// matching the original <c>GetKeysName</c>.
    /// </summary>
    public static string ForeignKeyStem(ForeignKeyModel foreignKey) =>
        string.Concat(foreignKey.ForeignKeyMemberColumns.Select(PropertyName));

    /// <summary>Name of the generated navigation property, e.g. <c>CountryIdObject</c>.</summary>
    public string ForeignKeyObjectProperty(ForeignKeyModel foreignKey) =>
        ToPascalCase(StripPrefix(ForeignKeyStem(foreignKey))) + "Object";

    /// <summary>Navigation property name without the <c>Object</c> suffix.</summary>
    public string ForeignKeyObjectStem(ForeignKeyModel foreignKey) =>
        ToPascalCase(StripPrefix(ForeignKeyStem(foreignKey)));

    // ---- Audit columns ----------------------------------------------------------------

    /// <summary>
    /// True when a column is handled by the framework base class rather than generated code.
    /// The original templates hard-coded CreatorId, UpdatorId, CreateDate and UpdateDate.
    /// </summary>
    public bool IsAuditColumn(ColumnModel column) =>
        _settings.AuditColumns.Any(a => string.Equals(a, column.Name, StringComparison.OrdinalIgnoreCase));

    /// <summary>Columns that generated properties, parameters and reader code are produced for.</summary>
    public IEnumerable<ColumnModel> GeneratedColumns(TableModel table) =>
        table.Columns.Where(c => !IsAuditColumn(c));

    public IEnumerable<ColumnModel> GeneratedNonKeyColumns(TableModel table) =>
        table.NonPrimaryKeyColumns.Where(c => !IsAuditColumn(c));

    /// <summary>
    /// The canonical projection order: primary key first, then the remaining non-audit columns
    /// in table order. Generated SELECT lists and the reader offsets in FillObject both use this,
    /// so the two stay aligned no matter where the key physically sits in the table.
    /// </summary>
    public IEnumerable<ColumnModel> OrderedGeneratedColumns(TableModel table)
    {
        foreach (var key in table.PrimaryKey?.MemberColumns ?? [])
        {
            yield return key;
        }

        foreach (var column in table.Columns)
        {
            if (!column.IsPrimaryKeyMember && !IsAuditColumn(column))
            {
                yield return column;
            }
        }
    }

    /// <summary>
    /// Audit columns present on the table, projected after the generated ones so the
    /// framework's FillBaseObject reads them at the offset the generated code passes it.
    /// </summary>
    public IEnumerable<ColumnModel> AuditColumnsOf(TableModel table) =>
        table.Columns.Where(IsAuditColumn);

    // ---- Mapping tables ---------------------------------------------------------------

    /// <summary>
    /// True when a table is a junction table and should use the relation data access base.
    /// The original rule tested the raw table name for an underscore, which matched every
    /// table once a <c>tbl_</c> prefix was configured; the prefix is stripped first here.
    /// </summary>
    public bool IsMappingTable(TableModel table) => StripPrefix(table.Name).Contains('_', StringComparison.Ordinal);

    // ---- Stored procedure names -------------------------------------------------------
    // Both the T-SQL emitter and the data access emitter call these, so the constants in the
    // DAL always match the procedures that are actually created.

    private string Proc(string name) => _settings.ProcedurePrefix + name;

    public string InsertProcedure(TableModel t) => Proc($"Insert{ClassName(t)}");

    public string UpdateProcedure(TableModel t) => Proc($"Update{ClassName(t)}");

    public string DeleteProcedure(TableModel t) => Proc($"Delete{ClassName(t)}");

    public string SelectByPrimaryKeyProcedure(TableModel t) => Proc($"Get{ClassName(t)}By{PrimaryKeyName(t)}");

    public string SelectAllProcedure(TableModel t) => Proc($"GetAll{ClassName(t)}");

    public string SelectPagedProcedure(TableModel t) => Proc($"GetPaged{ClassName(t)}");

    public string SelectByForeignKeyProcedure(TableModel t, ColumnModel c) => Proc($"Get{ClassName(t)}By{c.Name}");

    public string DeleteByForeignKeyProcedure(TableModel t, ColumnModel c) => Proc($"Delete{ClassName(t)}By{c.Name}");

    public string MaxProcedure(TableModel t) => Proc($"Get{ClassName(t)}Maximum{PrimaryKeyName(t)}");

    public string RowCountProcedure(TableModel t) => Proc($"Get{ClassName(t)}RowCount");

    public string ByQueryProcedure(TableModel t) => Proc($"Get{ClassName(t)}ByQuery");
}
