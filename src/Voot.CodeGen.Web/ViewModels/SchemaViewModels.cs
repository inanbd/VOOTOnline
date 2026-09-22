using Voot.CodeGen.Application.Models;
using Voot.CodeGen.Application.Services;
using Voot.CodeGen.Domain.Generation;
using Voot.CodeGen.Domain.Schema;
using Voot.CodeGen.Generation.Naming;

namespace Voot.CodeGen.Web.ViewModels;

/// <summary>What the schema browser renders: the picker plus the selected tables' structure.</summary>
public sealed record SchemaViewModel
{
    public required SchemaStructure Structure { get; init; }

    /// <summary>Resolves the class name and mapping-table rule using the project's own settings.</summary>
    public required NameResolver Names { get; init; }

    /// <summary>Table names currently ticked, used to render the checkbox state.</summary>
    public required HashSet<string> SelectedNames { get; init; }

    /// <summary>Set after SQL was run from this page; null on a plain view.</summary>
    public SchemaChangeResult? LastChange { get; init; }

    /// <summary>Recent SQL changes for this project, newest first.</summary>
    public IReadOnlyList<ChangeRequest> RecentChanges { get; init; } = [];

    /// <summary>The SQL to put back in the editor after a failed run, so it is not lost.</summary>
    public string? SqlText { get; init; }

    public string? Title { get; init; }

    public bool SelectAll => Structure.SelectAll;

    public bool HasSelection => Structure.Selected.Count > 0;
}

/// <summary>
/// Formatting for the schema browser. Kept out of <c>SqlTypeMap</c> on purpose: that throws on a
/// type the generator cannot map, and the browser has to be able to show whatever is really there.
/// </summary>
public static class SchemaDisplay
{
    /// <summary>The column's type as SQL Server would declare it.</summary>
    public static string SqlType(ColumnModel column) => SchemaFormat.SqlType(column);

    /// <summary>The column's default constraint, unwrapped, or null when it has none.</summary>
    public static string? Default(ColumnModel column) => SchemaFormat.Default(column);

    /// <summary>Short badges describing a column, e.g. PK, FK, identity.</summary>
    public static IEnumerable<string> Traits(ColumnModel column)
    {
        if (column.IsPrimaryKeyMember) yield return "PK";
        if (column.IsForeignKeyMember) yield return "FK";
        if (column.IsIdentity) yield return "identity";
        if (column.IsComputed) yield return "computed";
        if (column.IsRowVersion) yield return "rowversion";
    }

    /// <summary>The foreign key a column participates in, or null when it is not part of one.</summary>
    public static ForeignKeyModel? ForeignKeyFor(TableModel table, ColumnModel column) =>
        table.ForeignKeys.FirstOrDefault(fk =>
            fk.ForeignKeyMemberColumns.Any(c =>
                string.Equals(c.Name, column.Name, StringComparison.OrdinalIgnoreCase)));

    public static string ColumnList(IEnumerable<ColumnModel> columns) =>
        string.Join(", ", columns.Select(c => c.Name));
}
