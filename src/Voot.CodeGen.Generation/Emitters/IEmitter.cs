using Voot.CodeGen.Domain.Schema;

namespace Voot.CodeGen.Generation.Emitters;

/// <summary>
/// Produces the files for one of the original CodeSmith templates. One emitter corresponds
/// to one .cst file; the generator runs every applicable emitter over every table.
/// </summary>
public interface IEmitter
{
    /// <summary>Stable identifier recorded against each generated file, e.g. <c>BaseEntity</c>.</summary>
    string Name { get; }

    /// <summary>
    /// Whether this emitter produces anything for the table. Relation emitters apply only to
    /// mapping tables and the standard ones only to everything else.
    /// </summary>
    bool AppliesTo(TableModel table, GenerationContext context);

    IEnumerable<GeneratedFile> Emit(TableModel table, GenerationContext context);
}
