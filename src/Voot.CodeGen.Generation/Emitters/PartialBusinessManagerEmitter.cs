using Voot.CodeGen.Domain.Schema;

namespace Voot.CodeGen.Generation.Emitters;

/// <summary>
/// Port of <c>PartialBusinessManager.cst</c>. Emits the hand-editable half of the manager,
/// pre-seeded with the <c>Update</c> and <c>FillChilds</c> entry points the original provided.
/// </summary>
public sealed class PartialBusinessManagerEmitter : EmitterBase
{
    public override string Name => "PartialBusinessManager";

    public override bool AppliesTo(TableModel table, GenerationContext context) =>
        table.HasPrimaryKey && !context.Names.IsMappingTable(table);

    public override IEnumerable<GeneratedFile> Emit(TableModel table, GenerationContext context)
    {
        var names = context.Names;
        var className = names.ClassName(table);
        var managerName = className + "Manager";
        var objectVar = names.CamelCaseClassName(table) + "Object";
        var writer = context.NewWriter();

        WriteUsings(
            writer,
            "System",
            context.SqlClientNamespace,
            context.FrameworkNamespace,
            context.ExceptionsNamespace,
            context.EntitiesNamespace,
            context.EntityBaseReferenceNamespace,
            context.EntityListReferenceNamespace,
            context.DataAccessNamespace);

        using (context.NamespaceScope(writer, context.BusinessLogicNamespace))
        {
            WriteSummary(writer, $"Hand-written business logic for {className}.");
            using (writer.Block($"public partial class {managerName}"))
            {
                WriteSummary(
                    writer,
                    $"Persists a {className}. Add validation or cross-entity rules here, then",
                    "call UpdateBase to run the generated persistence.");
                using (writer.Block($"public bool Update({className} {objectVar})"))
                {
                    writer.Line($"return UpdateBase({objectVar});");
                }

                writer.Blank();

                WriteSummary(writer, $"Loads any additional children a {className} needs.");
                using (writer.Block($"public void FillChilds({className} {objectVar})"))
                {
                    writer.Line("// Load child collections that the generated manager does not know about.");
                }
            }
        }

        yield return new GeneratedFile
        {
            RelativePath = $"{context.BusinessLogicFolder}/{managerName}.cs",
            Content = writer.ToString(),
            Emitter = Name,
            TableName = table.Name
        };
    }
}
