using Voot.CodeGen.Domain.Schema;

namespace Voot.CodeGen.Generation.Emitters;

/// <summary>
/// Port of <c>PartialEntity.cst</c>. Emits an empty partial class as the place for
/// hand-written entity logic. Generated once as scaffolding; see the archive README.
/// </summary>
public sealed class PartialEntityEmitter : EmitterBase
{
    public override string Name => "PartialEntity";

    public override IEnumerable<GeneratedFile> Emit(TableModel table, GenerationContext context)
    {
        var className = context.Names.ClassName(table);
        var writer = context.NewWriter();

        WriteUsings(
            writer,
            "System",
            "System.Runtime.Serialization",
            context.IsLegacy ? "System.ServiceModel" : null,
            context.FrameworkNamespace,
            context.EntityBaseReferenceNamespace,
            context.EntityListReferenceNamespace);

        using (context.NamespaceScope(writer, context.EntitiesNamespace))
        {
            using (writer.Block($"public partial class {className}"))
            {
                writer.Line("// Hand-written members for this entity go here.");
            }
        }

        yield return new GeneratedFile
        {
            RelativePath = $"{context.EntityFolder}/{className}.cs",
            Content = writer.ToString(),
            Emitter = Name,
            TableName = table.Name
        };
    }
}
