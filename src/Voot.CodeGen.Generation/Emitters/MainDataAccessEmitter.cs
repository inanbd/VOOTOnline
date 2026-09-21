using Voot.CodeGen.Domain.Schema;

namespace Voot.CodeGen.Generation.Emitters;

/// <summary>
/// Port of <c>MainDataAccess.cst</c>. Emits the data access class for an ordinary table,
/// including paging, free-form query, max-key and row-count methods.
/// </summary>
public sealed class MainDataAccessEmitter : DataAccessEmitterBase
{
    public override string Name => "MainDataAccess";

    protected override string BaseClassName => "BaseDataAccess";

    protected override bool IncludeExtendedQueries => true;

    public override bool AppliesTo(TableModel table, GenerationContext context) =>
        table.HasPrimaryKey && !context.Names.IsMappingTable(table);
}
