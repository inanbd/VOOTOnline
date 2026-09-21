using Voot.CodeGen.Domain.Schema;
using Voot.CodeGen.Generation.Naming;

namespace Voot.CodeGen.Generation.Emitters;

/// <summary>
/// Port of <c>Entity.cst</c>. Emits the regenerated half of the entity: one navigation
/// property per foreign key, plus value equality on the primary key.
/// </summary>
public sealed class EntityEmitter : EmitterBase
{
    public override string Name => "Entity";

    public override IEnumerable<GeneratedFile> Emit(TableModel table, GenerationContext context)
    {
        var names = context.Names;
        var className = names.ClassName(table);
        var writer = context.NewWriter();

        WriteGeneratedHeader(writer, table);
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
            if (context.IsLegacy)
            {
                writer.Line("[Serializable]");
                writer.Line($"[DataContract(Name = \"{className}\", Namespace = \"{context.ContractNamespace}/entities\")]");
            }

            using (writer.Block($"public partial class {className} : {className}Base"))
            {
                WriteForeignKeyProperties(writer, table, context);
                WriteEquality(writer, table, context);
            }
        }

        yield return new GeneratedFile
        {
            RelativePath = $"{context.EntityBaseFolder}/{className}.cs",
            Content = writer.ToString(),
            Emitter = Name,
            TableName = table.Name
        };
    }

    private static void WriteForeignKeyProperties(CodeWriter writer, TableModel table, GenerationContext context)
    {
        var names = context.Names;

        using (writer.Region("External Properties"))
        {
            var emitted = new HashSet<string>(StringComparer.Ordinal);

            foreach (var foreignKey in table.ForeignKeys)
            {
                // Only relate to tables that are part of this generation pass.
                if (!context.Database.ContainsTable(foreignKey.PrimaryKeyTableName))
                {
                    continue;
                }

                var stem = names.ForeignKeyObjectStem(foreignKey);
                if (!emitted.Add(stem))
                {
                    continue;
                }

                var parentClass = names.StripPrefix(foreignKey.PrimaryKeyTableName);
                var field = $"_{stem}Object";
                var nullable = context.IsModern ? "?" : string.Empty;

                writer.Line($"private {parentClass}{nullable} {field} = null;");
                writer.Blank();
                WriteSummary(writer, $"Gets or sets the related <see cref=\"{parentClass}\"/>.");

                if (context.IsLegacy)
                {
                    writer.Line("[DataMember]");
                }

                using (writer.Block($"public {parentClass}{nullable} {stem}Object"))
                {
                    writer.Line($"get {{ return this.{field}; }}");
                    writer.Line($"set {{ this.{field} = value; }}");
                }

                writer.Blank();
            }
        }

        writer.Blank();
    }

    private static void WriteEquality(CodeWriter writer, TableModel table, GenerationContext context)
    {
        var className = context.Names.ClassName(table);
        var primaryKey = NameResolver.PrimaryKeyName(table);
        var nullable = context.IsModern ? "?" : string.Empty;

        using (writer.Region("Equality"))
        {
            using (writer.Block($"public override bool Equals(object{nullable} obj)"))
            {
                using (writer.Block($"if (obj is not {className} other)"))
                {
                    writer.Line("return false;");
                }

                writer.Blank();
                writer.Line($"return other.{primaryKey} == this.{primaryKey} && other.CustomPropertyMatch(this);");
            }

            writer.Blank();

            using (writer.Block("public override int GetHashCode()"))
            {
                writer.Line($"return this.{primaryKey}.GetHashCode();");
            }
        }
    }
}
