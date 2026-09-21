using Voot.CodeGen.Domain.Schema;
using Voot.CodeGen.Generation.Naming;

namespace Voot.CodeGen.Generation.Emitters;

/// <summary>
/// Port of <c>MainBusinessManager.cst</c>. Emits <c>{Entity}Manager</c>: RowState-driven
/// persistence, key and list retrieval, and recursive loading of foreign key children.
/// Mapping tables do not get a manager, matching the original generator.
/// </summary>
public sealed class MainBusinessManagerEmitter : EmitterBase
{
    public override string Name => "MainBusinessManager";

    public override bool AppliesTo(TableModel table, GenerationContext context) =>
        table.HasPrimaryKey && !context.Names.IsMappingTable(table);

    public override IEnumerable<GeneratedFile> Emit(TableModel table, GenerationContext context)
    {
        var names = context.Names;
        var className = names.ClassName(table);
        var managerName = className + "Manager";
        var accessName = className + "DataAccess";
        var listName = className + "List";
        var objectVar = names.CamelCaseClassName(table) + "Object";
        var primaryKey = NameResolver.PrimaryKeyColumn(table);
        var primaryKeyType = NameResolver.PrimaryKeyClrType(table, context.Style);
        var contextName = context.Settings.ContextName;
        var nullable = context.IsModern ? "?" : string.Empty;

        var writer = context.NewWriter();

        WriteGeneratedHeader(writer, table);
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
            WriteSummary(writer, $"Business logic for {className}.");
            using (writer.Block($"public partial class {managerName} : BaseManager"))
            {
                using (writer.Region("Constructors"))
                {
                    writer.Line($"public {managerName}({contextName} context) : base(context) {{ }}");
                    writer.Line($"public {managerName}(SqlTransaction transaction, {contextName} context) : base(transaction, context) {{ }}");
                }

                writer.Blank();

                WriteInsert(writer, className, accessName, objectVar, contextName);
                WriteUpdateBase(writer, className, accessName, objectVar, primaryKey, contextName);
                WriteDelete(writer, accessName, primaryKey, primaryKeyType, contextName);
                WriteGet(writer, context, table, className, accessName, objectVar, primaryKey, primaryKeyType, contextName, nullable);
                WriteGetAll(writer, context, className, accessName, listName, objectVar, contextName);
            }
        }

        yield return new GeneratedFile
        {
            RelativePath = $"{context.BusinessLogicBaseFolder}/{managerName}.cs",
            Content = writer.ToString(),
            Emitter = Name,
            TableName = table.Name
        };
    }

    private static void WriteInsert(
        CodeWriter writer, string className, string accessName, string objectVar, string contextName)
    {
        using (writer.Region("Insert Method"))
        {
            WriteSummary(writer, $"Inserts a new {className}.");
            using (writer.Block($"private bool Insert({className} {objectVar})"))
            {
                using (writer.Block($"using ({accessName} data = new {accessName}({contextName}))"))
                {
                    writer.Line($"long result = data.Insert({objectVar});");
                    writer.Line("return result > 0;");
                }
            }
        }

        writer.Blank();
    }

    private static void WriteUpdateBase(
        CodeWriter writer, string className, string accessName, string objectVar,
        ColumnModel primaryKey, string contextName)
    {
        using (writer.Region("Update Method"))
        {
            WriteSummary(
                writer,
                $"Persists a {className} according to its RowState:",
                "new rows are inserted, deleted rows removed, everything else updated.");
            using (writer.Block($"public bool UpdateBase({className} {objectVar})"))
            {
                using (writer.Block($"switch ({objectVar}.RowState)"))
                {
                    writer.Line("case BaseBusinessEntity.RowStateEnum.NewRow:");
                    writer.Indent().Line($"return Insert({objectVar});").Outdent();
                    writer.Line("case BaseBusinessEntity.RowStateEnum.DeletedRow:");
                    writer.Indent().Line($"return Delete({objectVar}.{primaryKey.Name});").Outdent();
                }

                writer.Blank();
                using (writer.Block($"using ({accessName} data = new {accessName}({contextName}))"))
                {
                    writer.Line($"return data.Update({objectVar}) > 0;");
                }
            }
        }

        writer.Blank();
    }

    private static void WriteDelete(
        CodeWriter writer, string accessName, ColumnModel primaryKey, string primaryKeyType, string contextName)
    {
        using (writer.Region("Delete Method"))
        {
            WriteSummary(writer, "Deletes a row by key.");
            using (writer.Block($"private bool Delete({primaryKeyType} _{primaryKey.Name})"))
            {
                using (writer.Block($"using ({accessName} data = new {accessName}({contextName}))"))
                {
                    writer.Line($"return data.Delete(_{primaryKey.Name}) > 0;");
                }
            }
        }

        writer.Blank();
    }

    private static void WriteGet(
        CodeWriter writer, GenerationContext context, TableModel table, string className, string accessName,
        string objectVar, ColumnModel primaryKey, string primaryKeyType, string contextName, string nullable)
    {
        using (writer.Region($"Get By {primaryKey.Name} Method"))
        {
            WriteSummary(writer, $"Retrieves a {className} by key.");
            using (writer.Block($"public {className}{nullable} Get({primaryKeyType} _{primaryKey.Name})"))
            {
                using (writer.Block($"using ({accessName} data = new {accessName}({contextName}))"))
                {
                    writer.Line($"return data.Get(_{primaryKey.Name});");
                }
            }

            writer.Blank();

            WriteSummary(writer, $"Retrieves a {className} by key, optionally loading its related entities.");
            using (writer.Block($"public {className}{nullable} Get({primaryKeyType} _{primaryKey.Name}, bool fillChild)"))
            {
                writer.Line($"{className}{nullable} {objectVar} = Get(_{primaryKey.Name});");
                writer.Blank();
                using (writer.Block($"if ({objectVar} != null && fillChild)"))
                {
                    writer.Line($"Fill{className}WithChilds({objectVar}, fillChild);");
                }

                writer.Blank();
                writer.Line($"return {objectVar};");
            }

            writer.Blank();
            WriteFillChildren(writer, context, table, className, objectVar, contextName, nullable);
        }

        writer.Blank();
    }

    private static void WriteFillChildren(
        CodeWriter writer, GenerationContext context, TableModel table, string className, string objectVar,
        string contextName, string nullable)
    {
        var names = context.Names;

        WriteSummary(writer, $"Loads the entities referenced by this {className}'s foreign keys.");
        using (writer.Block($"private void Fill{className}WithChilds({className}{nullable} {objectVar}, bool fillChilds)"))
        {
            using (writer.Block($"if ({objectVar} == null)"))
            {
                writer.Line("return;");
            }

            writer.Blank();

            var emitted = new HashSet<string>(StringComparer.Ordinal);

            foreach (var foreignKey in table.ForeignKeys)
            {
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
                var parentManager = parentClass + "Manager";
                var managerVar = NameResolver.ToCamelCase(parentClass) + "Manager";

                writer.Line($"// {stem}Object is a {parentClass} resolved through {stem}.");
                using (writer.Block($"using ({parentManager} {managerVar} = new {parentManager}({contextName}))"))
                {
                    writer.Line($"{objectVar}.{stem}Object = {managerVar}.Get({objectVar}.{stem}, fillChilds);");
                }

                writer.Blank();
            }
        }
    }

    private static void WriteGetAll(
        CodeWriter writer, GenerationContext context, string className, string accessName, string listName,
        string objectVar, string contextName)
    {
        var settings = context.Settings;

        using (writer.Region("GetAll Method"))
        {
            WriteSummary(writer, $"Retrieves every {className}.");
            using (writer.Block($"public {listName} GetAll()"))
            {
                using (writer.Block($"using ({accessName} data = new {accessName}({contextName}))"))
                {
                    writer.Line("return data.GetAll();");
                }
            }

            writer.Blank();

            WriteSummary(writer, $"Retrieves every {className}, optionally loading related entities.");
            using (writer.Block($"public {listName} GetAll(bool fillChild)"))
            {
                writer.Line($"{listName} list;");
                writer.Blank();
                using (writer.Block($"using ({accessName} data = new {accessName}({contextName}))"))
                {
                    writer.Line("list = data.GetAll();");
                }

                writer.Blank();
                using (writer.Block("if (fillChild)"))
                {
                    using (writer.Block($"foreach ({className} {objectVar} in list)"))
                    {
                        writer.Line($"Fill{className}WithChilds({objectVar}, fillChild);");
                    }
                }

                writer.Blank();
                writer.Line("return list;");
            }

            if (settings.IncludeSelectPaged)
            {
                writer.Blank();
                WriteSummary(writer, $"Retrieves a page of {className} rows.");
                using (writer.Block($"public {listName} GetPaged(PagedRequest request)"))
                {
                    using (writer.Block($"using ({accessName} data = new {accessName}({contextName}))"))
                    {
                        writer.Line("return data.GetPaged(request);");
                    }
                }
            }

            if (settings.IncludeSelectByQuery)
            {
                writer.Blank();
                WriteSummary(writer, $"Retrieves every {className} matching a WHERE fragment.");
                using (writer.Block($"public {listName} GetByQuery(string query)"))
                {
                    using (writer.Block($"using ({accessName} data = new {accessName}({contextName}))"))
                    {
                        writer.Line("return data.GetByQuery(query);");
                    }
                }
            }
        }

        writer.Blank();
    }
}
