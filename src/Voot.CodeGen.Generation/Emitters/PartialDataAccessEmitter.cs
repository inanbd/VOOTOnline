using Voot.CodeGen.Domain.Schema;

namespace Voot.CodeGen.Generation.Emitters;

/// <summary>
/// Port of <c>PartialDataAccess.cst</c> and <c>PartialRelationDataAccess.cst</c>, which were
/// identical. Emits the hand-editable half of the data access class.
/// </summary>
public sealed class PartialDataAccessEmitter : EmitterBase
{
    public override string Name => "PartialDataAccess";

    public override IEnumerable<GeneratedFile> Emit(TableModel table, GenerationContext context)
    {
        var accessName = context.Names.ClassName(table) + "DataAccess";
        var writer = context.NewWriter();

        WriteUsings(
            writer,
            "System",
            "System.Data",
            context.SqlClientNamespace,
            context.FrameworkNamespace,
            context.ExceptionsNamespace,
            context.EntitiesNamespace,
            context.EntityBaseReferenceNamespace,
            context.EntityListReferenceNamespace);

        using (context.NamespaceScope(writer, context.DataAccessNamespace))
        {
            using (writer.Block($"public partial class {accessName}"))
            {
                // These live here rather than in the regenerated half so the constructor set
                // stays editable. The base class must provide a parameterless constructor and
                // one taking a connection string; the archive README lists both.
                using (writer.Region("Constructors"))
                {
                    writer.Line($"public {accessName}() {{ }}");
                    writer.Line($"public {accessName}(string ConnectionStr) : base(ConnectionStr) {{ }}");
                }

                writer.Blank();
                writer.Line("// Hand-written queries for this table go here.");
            }
        }

        yield return new GeneratedFile
        {
            RelativePath = $"{context.DataAccessFolder}/{accessName}.cs",
            Content = writer.ToString(),
            Emitter = Name,
            TableName = table.Name
        };
    }
}
