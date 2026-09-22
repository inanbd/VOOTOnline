namespace Voot.CodeGen.Application.Models;

/// <summary>
/// A row in the table picker. Read with one cheap catalog query so the picker does not pay
/// for the full column, key and index read that <see cref="Abstractions.ISchemaReader.ReadAsync"/> does.
/// </summary>
/// <param name="Owner">Schema the table belongs to, e.g. <c>dbo</c>.</param>
/// <param name="Name">Table name as it appears in the database.</param>
/// <param name="ColumnCount">Number of columns.</param>
/// <param name="HasPrimaryKey">
/// False when the generator would skip the table; surfaced in the picker so the reason a table
/// never appears in an archive is visible without running a generation.
/// </param>
public readonly record struct TableSummary(string Owner, string Name, int ColumnCount, bool HasPrimaryKey)
{
    public string QualifiedName => $"[{Owner}].[{Name}]";
}
