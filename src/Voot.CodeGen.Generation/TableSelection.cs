using Voot.CodeGen.Domain.Schema;

namespace Voot.CodeGen.Generation;

/// <summary>
/// Restricts a generation pass to some tables. The generator still sees the whole schema, so a
/// generated table's navigation properties and managers can refer to tables left out of the pass.
/// </summary>
public sealed class TableSelection
{
    private readonly HashSet<string> _keys;

    public TableSelection(IEnumerable<string> tableKeys, IEnumerable<string>? droppedTables = null)
    {
        ArgumentNullException.ThrowIfNull(tableKeys);

        _keys = new HashSet<string>(tableKeys, StringComparer.OrdinalIgnoreCase);
        DroppedTables = [.. droppedTables ?? []];
    }

    /// <summary>Schema-qualified keys (<c>owner.name</c>) of the tables to generate.</summary>
    public IReadOnlyCollection<string> TableKeys => _keys;

    /// <summary>Tables removed since the baseline, listed in the archive so their files can be deleted.</summary>
    public IReadOnlyList<string> DroppedTables { get; }

    public bool Includes(TableModel table) => _keys.Contains(KeyOf(table));

    public static string KeyOf(TableModel table)
    {
        ArgumentNullException.ThrowIfNull(table);
        return $"{table.Owner}.{table.Name}";
    }
}
