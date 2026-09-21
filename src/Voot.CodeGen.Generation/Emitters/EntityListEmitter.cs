using Voot.CodeGen.Domain.Schema;

namespace Voot.CodeGen.Generation.Emitters;

/// <summary>
/// Port of <c>EntityList.cst</c>. Emits <c>{Entity}List</c>, the typed collection used as the
/// return type of every multi-row data access method.
/// </summary>
public sealed class EntityListEmitter : EmitterBase
{
    public override string Name => "EntityList";

    public override IEnumerable<GeneratedFile> Emit(TableModel table, GenerationContext context)
    {
        var className = context.Names.ClassName(table);
        var listName = className + "List";
        var writer = context.NewWriter();

        WriteGeneratedHeader(writer, table);
        WriteUsings(
            writer,
            "System",
            "System.Collections.Generic",
            "System.Runtime.Serialization",
            context.IsLegacy ? "System.ServiceModel" : null,
            context.FrameworkNamespace,
            context.EntitiesNamespace);

        using (context.NamespaceScope(writer, context.EntityListNamespace))
        {
            if (context.IsLegacy)
            {
                writer.Line("[Serializable]");
                writer.Line(
                    $"[CollectionDataContract(Name = \"{listName}\", Namespace = \"{context.ContractNamespace}/list\")]");
            }

            using (writer.Block($"public class {listName} : BaseCollection<{className}>"))
            {
                using (writer.Region("Constructors"))
                {
                    writer.Line($"public {listName}() : base() {{ }}");
                    writer.Line($"public {listName}({className}[] list) : base(list) {{ }}");
                    writer.Line($"public {listName}(List<{className}> list) : base(list) {{ }}");
                }

                writer.Blank();

                using (writer.Region("Custom Methods"))
                {
                    writer.Line("// Hand-written collection helpers go here.");
                }
            }
        }

        yield return new GeneratedFile
        {
            RelativePath = $"{context.EntityListFolder}/{listName}.cs",
            Content = writer.ToString(),
            Emitter = Name,
            TableName = table.Name
        };
    }
}
