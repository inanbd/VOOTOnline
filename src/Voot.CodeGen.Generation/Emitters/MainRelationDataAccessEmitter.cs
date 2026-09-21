using Voot.CodeGen.Domain.Schema;

namespace Voot.CodeGen.Generation.Emitters;

/// <summary>
/// Port of <c>MainRelationDataAccess.cst</c>. Emits the data access class for a junction
/// table: the same CRUD surface over <c>BaseRelationData</c>, without paging or row counts.
/// </summary>
public sealed class MainRelationDataAccessEmitter : DataAccessEmitterBase
{
    public override string Name => "MainRelationDataAccess";

    protected override string BaseClassName => "BaseRelationData";

    protected override bool IncludeExtendedQueries => false;

    public override bool AppliesTo(TableModel table, GenerationContext context) =>
        table.HasPrimaryKey && context.Names.IsMappingTable(table);
}
