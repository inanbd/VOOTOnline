using Voot.CodeGen.Domain.Schema;
using Voot.CodeGen.Generation.Naming;

namespace Voot.CodeGen.Generation.Emitters;

/// <summary>
/// Port of <c>BaseEntity.cst</c>. Emits <c>{Entity}Base</c>: the column enum, property-name
/// constants, backing fields, change-tracked properties, <c>Clone</c> and <c>GetObjectData</c>.
/// </summary>
public sealed class BaseEntityEmitter : EmitterBase
{
    public override string Name => "BaseEntity";

    public override IEnumerable<GeneratedFile> Emit(TableModel table, GenerationContext context)
    {
        var names = context.Names;
        var className = names.ClassName(table);
        var baseName = className + "Base";
        var columns = names.GeneratedColumns(table).ToList();
        var writer = context.NewWriter();

        WriteGeneratedHeader(writer, table);
        WriteUsings(
            writer,
            "System",
            "System.Runtime.Serialization",
            context.IsLegacy ? "System.ServiceModel" : null,
            context.FrameworkNamespace);

        using (context.NamespaceScope(writer, context.EntityBaseNamespace))
        {
            if (context.IsLegacy)
            {
                writer.Line("[Serializable]");
                writer.Line($"[DataContract(Name = \"{baseName}\", Namespace = \"{context.ContractNamespace}/entities\")]");
            }

            using (writer.Block($"public class {baseName} : BaseBusinessEntity"))
            {
                WriteColumnsEnum(writer, columns);
                WriteConstants(writer, columns);
                WriteFields(writer, columns, context);
                WriteProperties(writer, columns, context);
                WriteClone(writer, baseName, columns);
                WriteGetObjectData(writer, baseName, columns);
            }
        }

        yield return new GeneratedFile
        {
            RelativePath = $"{context.EntityBaseFolder}/{baseName}.cs",
            Content = writer.ToString(),
            Emitter = Name,
            TableName = table.Name
        };
    }

    private static void WriteColumnsEnum(CodeWriter writer, IReadOnlyList<ColumnModel> columns)
    {
        using (writer.Region("Enum Collection"))
        {
            using (writer.Block("public enum Columns"))
            {
                for (var i = 0; i < columns.Count; i++)
                {
                    var comma = i < columns.Count - 1 ? "," : string.Empty;
                    writer.Line($"{NameResolver.PropertyName(columns[i])} = {i}{comma}");
                }
            }
        }

        writer.Blank();
    }

    private static void WriteConstants(CodeWriter writer, IReadOnlyList<ColumnModel> columns)
    {
        using (writer.Region("Constants"))
        {
            foreach (var column in columns)
            {
                var property = NameResolver.PropertyName(column);
                writer.Line($"public const string Property_{property} = \"{property}\";");
            }
        }

        writer.Blank();
    }

    private static void WriteFields(CodeWriter writer, IReadOnlyList<ColumnModel> columns, GenerationContext context)
    {
        using (writer.Region("Private Data Types"))
        {
            foreach (var column in columns)
            {
                var type = SqlTypeMap.ClrType(column, context.Style);
                writer.Line($"private {type} _{NameResolver.PropertyName(column)};");
            }
        }

        writer.Blank();
    }

    private static void WriteProperties(CodeWriter writer, IReadOnlyList<ColumnModel> columns, GenerationContext context)
    {
        using (writer.Region("Properties"))
        {
            foreach (var column in columns)
            {
                var property = NameResolver.PropertyName(column);
                var type = SqlTypeMap.ClrType(column, context.Style);

                if (context.IsLegacy)
                {
                    writer.Line("[DataMember]");
                }

                using (writer.Block($"public {type} {property}"))
                {
                    writer.Line($"get {{ return _{property}; }}");
                    using (writer.Block("set"))
                    {
                        writer.Line(
                            $"PropertyChangingEventArgs args = new PropertyChangingEventArgs(Property_{property}, value, _{property});");
                        using (writer.Block("if (PropertyChanging(args))"))
                        {
                            writer.Line($"_{property} = value;");
                            writer.Line("PropertyChanged(args);");
                        }
                    }
                }

                writer.Blank();
            }
        }

        writer.Blank();
    }

    private static void WriteClone(CodeWriter writer, string baseName, IReadOnlyList<ColumnModel> columns)
    {
        using (writer.Region("Cloning Base Objects"))
        {
            using (writer.Block($"public {baseName} Clone()"))
            {
                writer.Line($"{baseName} newObj = new {baseName}();");
                writer.Line("base.CloneBase(newObj);");
                writer.Blank();

                foreach (var column in columns)
                {
                    var property = NameResolver.PropertyName(column);
                    writer.Line($"newObj.{property} = this.{property};");
                }

                writer.Blank();
                writer.Line("return newObj;");
            }
        }

        writer.Blank();
    }

    private static void WriteGetObjectData(CodeWriter writer, string baseName, IReadOnlyList<ColumnModel> columns)
    {
        using (writer.Region("Serialization"))
        {
            using (writer.Block("public override void GetObjectData(SerializationInfo info, StreamingContext context)"))
            {
                writer.Line("base.GetObjectData(info, context);");
                writer.Blank();

                foreach (var column in columns)
                {
                    var property = NameResolver.PropertyName(column);
                    writer.Line($"info.AddValue({baseName}.Property_{property}, {property});");
                }
            }
        }
    }
}
