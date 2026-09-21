using Voot.CodeGen.Domain.Schema;
using Voot.CodeGen.Generation.Naming;

namespace Voot.CodeGen.Generation.Emitters;

/// <summary>
/// Shared body of the standard and relation data access emitters. The two originals
/// (<c>MainDataAccess.cst</c> and <c>MainRelationDataAccess.cst</c>) were near-identical
/// copies; they differ only in the base class and in which query methods are emitted.
/// </summary>
public abstract class DataAccessEmitterBase : EmitterBase
{
    /// <summary>Base class of the generated data access type.</summary>
    protected abstract string BaseClassName { get; }

    /// <summary>
    /// Relation tables get only the key-based lookups; standard tables also get paging,
    /// free-form query, max-key and row-count methods.
    /// </summary>
    protected abstract bool IncludeExtendedQueries { get; }

    public override IEnumerable<GeneratedFile> Emit(TableModel table, GenerationContext context)
    {
        var names = context.Names;
        var className = names.ClassName(table);
        var baseEntity = className + "Base";
        var listName = className + "List";
        var accessName = className + "DataAccess";
        var objectVar = names.CamelCaseClassName(table) + "Object";
        var primaryKey = NameResolver.PrimaryKeyColumn(table);
        var primaryKeyName = primaryKey.Name;
        var primaryKeyType = NameResolver.PrimaryKeyClrType(table, context.Style);
        var settings = context.Settings;

        var writer = context.NewWriter();

        WriteGeneratedHeader(writer, table);
        WriteUsings(
            writer,
            "System",
            "System.Data",
            context.SqlClientNamespace,
            context.FrameworkNamespace,
            context.FrameworkDataAccessNamespace,
            context.ExceptionsNamespace,
            context.EntitiesNamespace,
            context.EntityBaseReferenceNamespace,
            context.EntityListReferenceNamespace);

        using (context.NamespaceScope(writer, context.DataAccessNamespace))
        {
            using (writer.Block($"public partial class {accessName} : {BaseClassName}"))
            {
                WriteConstants(writer, table, context, primaryKey);
                WriteConstructors(writer, accessName, settings.ContextName);
                WriteAddCommonParams(writer, table, context, baseEntity, objectVar);

                if (settings.IncludeInsert)
                {
                    WriteInsert(writer, table, context, className, baseEntity, objectVar, primaryKey, primaryKeyType);
                }

                if (settings.IncludeUpdate)
                {
                    WriteUpdate(writer, context, table, className, baseEntity, objectVar, primaryKey);
                }

                if (settings.IncludeDelete)
                {
                    WriteDelete(writer, context, table, className, baseEntity, primaryKey, primaryKeyType);
                }

                if (settings.IncludeSelect)
                {
                    WriteGetByPrimaryKey(writer, context, table, className, baseEntity, primaryKey, primaryKeyType);
                }

                WriteQueryMethods(writer, table, context, className, baseEntity, listName, primaryKey, primaryKeyType);
                WriteFillMethods(writer, table, context, className, baseEntity, listName, objectVar);
            }
        }

        yield return new GeneratedFile
        {
            RelativePath = $"{context.DataAccessBaseFolder}/{accessName}.cs",
            Content = writer.ToString(),
            Emitter = Name,
            TableName = table.Name
        };
    }

    private void WriteConstants(CodeWriter writer, TableModel table, GenerationContext context, ColumnModel primaryKey)
    {
        var names = context.Names;
        var upper = names.UpperCaseClassName(table);
        var settings = context.Settings;

        using (writer.Region("Constants"))
        {
            writer.LineIf(settings.IncludeInsert,
                $"private const string INSERT{upper} = \"{names.InsertProcedure(table)}\";");
            writer.LineIf(settings.IncludeUpdate,
                $"private const string UPDATE{upper} = \"{names.UpdateProcedure(table)}\";");
            writer.LineIf(settings.IncludeDelete,
                $"private const string DELETE{upper} = \"{names.DeleteProcedure(table)}\";");
            writer.LineIf(settings.IncludeSelect,
                $"private const string GET{upper}BY{NameResolver.ToUpperCase(primaryKey.Name)} = \"{names.SelectByPrimaryKeyProcedure(table)}\";");
            writer.LineIf(settings.IncludeSelectAll,
                $"private const string GETALL{upper} = \"{names.SelectAllProcedure(table)}\";");

            if (IncludeExtendedQueries && settings.IncludeSelectPaged)
            {
                writer.Line($"private const string GETPAGED{upper} = \"{names.SelectPagedProcedure(table)}\";");
            }

            if (settings.IncludeSelectByForeignKey)
            {
                foreach (var column in ForeignKeyLookupColumns(table, primaryKey))
                {
                    writer.Line(
                        $"private const string GET{upper}BY{NameResolver.ToUpperCase(column.Name)} = \"{names.SelectByForeignKeyProcedure(table, column)}\";");
                }
            }

            if (IncludeExtendedQueries && settings.IncludeMaxAndRowCount)
            {
                writer.Line(
                    $"private const string GET{upper}MAXIMUM{NameResolver.ToUpperCase(primaryKey.Name)} = \"{names.MaxProcedure(table)}\";");
                writer.Line($"private const string GET{upper}ROWCOUNT = \"{names.RowCountProcedure(table)}\";");
            }

            if (IncludeExtendedQueries && settings.IncludeSelectByQuery)
            {
                writer.Line($"private const string GET{upper}BYQUERY = \"{names.ByQueryProcedure(table)}\";");
            }
        }

        writer.Blank();
    }

    /// <summary>Foreign key columns that get their own lookup, excluding the primary key itself.</summary>
    protected static IEnumerable<ColumnModel> ForeignKeyLookupColumns(TableModel table, ColumnModel primaryKey) =>
        table.ForeignKeyColumns.Where(c => !string.Equals(c.Name, primaryKey.Name, StringComparison.OrdinalIgnoreCase));

    private static void WriteConstructors(CodeWriter writer, string accessName, string contextName)
    {
        using (writer.Region("Constructors"))
        {
            writer.Line($"public {accessName}({contextName} context) : base(context) {{ }}");
            writer.Line($"public {accessName}(SqlTransaction transaction, {contextName} context) : base(transaction, context) {{ }}");
        }

        writer.Blank();
    }

    private static void WriteAddCommonParams(
        CodeWriter writer, TableModel table, GenerationContext context, string baseEntity, string objectVar)
    {
        using (writer.Region("AddCommonParams Method"))
        {
            WriteSummary(writer, "Adds the non-key parameters shared by insert and update.");
            using (writer.Block($"private void AddCommonParams(SqlCommand cmd, {baseEntity} {objectVar})"))
            {
                foreach (var column in context.Names.GeneratedNonKeyColumns(table))
                {
                    writer.Line($"AddParameter(cmd, {ParameterFactory.InParameterFromObject(column, baseEntity, objectVar)});");
                }
            }
        }

        writer.Blank();
    }

    private static void WriteInsert(
        CodeWriter writer, TableModel table, GenerationContext context, string className, string baseEntity,
        string objectVar, ColumnModel primaryKey, string primaryKeyType)
    {
        var upper = context.Names.UpperCaseClassName(table);

        using (writer.Region("Insert Method"))
        {
            WriteSummary(writer, $"Inserts a {className}.", "<returns>Number of rows affected.</returns>");
            using (writer.Block($"public long Insert({baseEntity} {objectVar})"))
            {
                using (writer.Block("try"))
                {
                    writer.Line($"SqlCommand cmd = GetSPCommand(INSERT{upper});");
                    writer.Blank();

                    if (primaryKey.IsIdentity)
                    {
                        writer.Line($"AddParameter(cmd, {ParameterFactory.OutParameter(primaryKey, baseEntity)});");
                        writer.Line($"AddCommonParams(cmd, {objectVar});");
                        writer.Blank();
                        writer.Line("long result = InsertRecord(cmd);");
                        using (writer.Block("if (result > 0)"))
                        {
                            writer.Line($"{objectVar}.RowState = BaseBusinessEntity.RowStateEnum.NormalRow;");
                            writer.Line(
                                $"{objectVar}.{primaryKey.Name} = ({primaryKeyType})GetOutParameter(cmd, {baseEntity}.Property_{primaryKey.Name});");
                        }
                    }
                    else
                    {
                        writer.Line(
                            $"AddParameter(cmd, {ParameterFactory.InParameterFromObject(primaryKey, baseEntity, objectVar)});");
                        writer.Line($"AddCommonParams(cmd, {objectVar});");
                        writer.Blank();
                        writer.Line("long result = InsertRecord(cmd);");
                        using (writer.Block("if (result > 0)"))
                        {
                            writer.Line($"{objectVar}.RowState = BaseBusinessEntity.RowStateEnum.NormalRow;");
                        }
                    }

                    writer.Blank();
                    writer.Line("return result;");
                }

                using (writer.Block("catch (SqlException x)"))
                {
                    writer.Line($"throw new ObjectInsertException({objectVar}, x);");
                }
            }
        }

        writer.Blank();
    }

    private static void WriteUpdate(
        CodeWriter writer, GenerationContext context, TableModel table, string className, string baseEntity,
        string objectVar, ColumnModel primaryKey)
    {
        var upper = context.Names.UpperCaseClassName(table);

        using (writer.Region("Update Method"))
        {
            WriteSummary(writer, $"Updates a {className}.", "<returns>Number of rows affected.</returns>");
            using (writer.Block($"public long Update({baseEntity} {objectVar})"))
            {
                using (writer.Block("try"))
                {
                    writer.Line($"SqlCommand cmd = GetSPCommand(UPDATE{upper});");
                    writer.Blank();
                    writer.Line($"AddParameter(cmd, {ParameterFactory.InParameterFromObject(primaryKey, baseEntity, objectVar)});");
                    writer.Line($"AddCommonParams(cmd, {objectVar});");
                    writer.Blank();
                    writer.Line("long result = UpdateRecord(cmd);");
                    using (writer.Block("if (result > 0)"))
                    {
                        writer.Line($"{objectVar}.RowState = BaseBusinessEntity.RowStateEnum.NormalRow;");
                    }

                    writer.Blank();
                    writer.Line("return result;");
                }

                using (writer.Block("catch (SqlException x)"))
                {
                    writer.Line($"throw new ObjectUpdateException({objectVar}, x);");
                }
            }
        }

        writer.Blank();
    }

    private static void WriteDelete(
        CodeWriter writer, GenerationContext context, TableModel table, string className, string baseEntity,
        ColumnModel primaryKey, string primaryKeyType)
    {
        var upper = context.Names.UpperCaseClassName(table);

        using (writer.Region("Delete Method"))
        {
            WriteSummary(writer, $"Deletes a {className} by its key.", "<returns>Number of rows affected.</returns>");
            using (writer.Block($"public long Delete({primaryKeyType} _{primaryKey.Name})"))
            {
                using (writer.Block("try"))
                {
                    writer.Line($"SqlCommand cmd = GetSPCommand(DELETE{upper});");
                    writer.Blank();
                    writer.Line($"AddParameter(cmd, {ParameterFactory.InParameterFromLocal(primaryKey, baseEntity)});");
                    writer.Blank();
                    writer.Line("return DeleteRecord(cmd);");
                }

                using (writer.Block("catch (SqlException x)"))
                {
                    writer.Line($"throw new ObjectDeleteException(typeof({className}), _{primaryKey.Name}, x);");
                }
            }
        }

        writer.Blank();
    }

    private static void WriteGetByPrimaryKey(
        CodeWriter writer, GenerationContext context, TableModel table, string className, string baseEntity,
        ColumnModel primaryKey, string primaryKeyType)
    {
        var upper = context.Names.UpperCaseClassName(table);
        var nullable = context.IsModern ? "?" : string.Empty;

        using (writer.Region($"Get By {primaryKey.Name} Method"))
        {
            WriteSummary(writer, $"Retrieves a {className} by its key.", $"<returns>The {className}, or null when not found.</returns>");
            using (writer.Block($"public {className}{nullable} Get({primaryKeyType} _{primaryKey.Name})"))
            {
                using (writer.Block($"using (SqlCommand cmd = GetSPCommand(GET{upper}BY{NameResolver.ToUpperCase(primaryKey.Name)}))"))
                {
                    writer.Line($"AddParameter(cmd, {ParameterFactory.InParameterFromLocal(primaryKey, baseEntity)});");
                    writer.Blank();
                    writer.Line("return GetObject(cmd);");
                }
            }
        }

        writer.Blank();
    }

    private void WriteQueryMethods(
        CodeWriter writer, TableModel table, GenerationContext context, string className, string baseEntity,
        string listName, ColumnModel primaryKey, string primaryKeyType)
    {
        var names = context.Names;
        var upper = names.UpperCaseClassName(table);
        var settings = context.Settings;

        using (writer.Region("Query Methods"))
        {
            if (settings.IncludeSelectAll)
            {
                WriteSummary(writer, $"Retrieves every {className}.");
                using (writer.Block($"public {listName} GetAll()"))
                {
                    using (writer.Block($"using (SqlCommand cmd = GetSPCommand(GETALL{upper}))"))
                    {
                        writer.Line("return GetList(cmd, ALL_AVAILABLE_RECORDS);");
                    }
                }

                writer.Blank();
            }

            if (settings.IncludeSelectByForeignKey)
            {
                foreach (var column in ForeignKeyLookupColumns(table, primaryKey))
                {
                    var columnType = SqlTypeMap.ClrType(column, context.Style);
                    WriteSummary(writer, $"Retrieves every {className} matching {column.Name}.");
                    using (writer.Block($"public {listName} GetBy{column.Name}({columnType} _{column.Name})"))
                    {
                        using (writer.Block($"using (SqlCommand cmd = GetSPCommand(GET{upper}BY{NameResolver.ToUpperCase(column.Name)}))"))
                        {
                            writer.Line($"AddParameter(cmd, {ParameterFactory.InParameterFromLocal(column, baseEntity)});");
                            writer.Line("return GetList(cmd, ALL_AVAILABLE_RECORDS);");
                        }
                    }

                    writer.Blank();
                }
            }

            if (IncludeExtendedQueries && settings.IncludeSelectPaged)
            {
                WriteSummary(writer, $"Retrieves a page of {className} rows and sets the total row count on the request.");
                using (writer.Block($"public {listName} GetPaged(PagedRequest request)"))
                {
                    using (writer.Block($"using (SqlCommand cmd = GetSPCommand(GETPAGED{upper}))"))
                    {
                        writer.Line("AddParameter(cmd, pInt32Out(\"TotalRows\"));");
                        writer.Line("AddParameter(cmd, pInt32(\"PageIndex\", request.PageIndex));");
                        writer.Line("AddParameter(cmd, pInt32(\"RowPerPage\", request.RowPerPage));");
                        writer.Line("AddParameter(cmd, pNVarChar(\"WhereClause\", 4000, request.WhereClause));");
                        writer.Line("AddParameter(cmd, pNVarChar(\"SortColumn\", 128, request.SortColumn));");
                        writer.Line("AddParameter(cmd, pNVarChar(\"SortOrder\", 4, request.SortOrder));");
                        writer.Blank();
                        writer.Line($"{listName} list = GetList(cmd, ALL_AVAILABLE_RECORDS);");
                        writer.Line("request.TotalRows = Convert.ToInt32(GetOutParameter(cmd, \"TotalRows\"));");
                        writer.Line("return list;");
                    }
                }

                writer.Blank();
            }

            if (IncludeExtendedQueries && settings.IncludeSelectByQuery)
            {
                WriteSummary(
                    writer,
                    $"Retrieves every {className} matching a caller-supplied WHERE fragment.",
                    "<remarks>",
                    "The fragment is concatenated into dynamic SQL by the stored procedure, so it must",
                    "never be built from unvalidated user input.",
                    "</remarks>");
                using (writer.Block($"public {listName} GetByQuery(string query)"))
                {
                    using (writer.Block($"using (SqlCommand cmd = GetSPCommand(GET{upper}BYQUERY))"))
                    {
                        writer.Line("AddParameter(cmd, pNVarChar(\"Query\", 4000, query));");
                        writer.Line("return GetList(cmd, ALL_AVAILABLE_RECORDS);");
                    }
                }

                writer.Blank();
            }

            if (IncludeExtendedQueries && settings.IncludeMaxAndRowCount)
            {
                WriteSummary(writer, $"Returns the highest {primaryKey.Name} currently stored.");
                using (writer.Block($"public {primaryKeyType} GetMax{primaryKey.Name}()"))
                {
                    writer.Line($"{primaryKeyType} value = default({primaryKeyType});");
                    using (writer.Block($"using (SqlCommand cmd = GetSPCommand(GET{upper}MAXIMUM{NameResolver.ToUpperCase(primaryKey.Name)}))"))
                    {
                        writer.Line("SqlDataReader reader;");
                        writer.Line($"value = ({primaryKeyType})SelectRecords(cmd, out reader);");
                        writer.Line("reader.Close();");
                        writer.Line("reader.Dispose();");
                    }

                    writer.Blank();
                    writer.Line("return value;");
                }

                writer.Blank();

                WriteSummary(writer, $"Returns the total number of {className} rows.");
                using (writer.Block($"public {primaryKeyType} GetRowCount()"))
                {
                    writer.Line($"{primaryKeyType} value = default({primaryKeyType});");
                    using (writer.Block($"using (SqlCommand cmd = GetSPCommand(GET{upper}ROWCOUNT))"))
                    {
                        writer.Line("SqlDataReader reader;");
                        writer.Line($"value = ({primaryKeyType})SelectRecords(cmd, out reader);");
                        writer.Line("reader.Close();");
                        writer.Line("reader.Dispose();");
                    }

                    writer.Blank();
                    writer.Line("return value;");
                }
            }
        }

        writer.Blank();
    }

    private static void WriteFillMethods(
        CodeWriter writer, TableModel table, GenerationContext context, string className, string baseEntity,
        string listName, string objectVar)
    {
        var names = context.Names;
        var columns = names.OrderedGeneratedColumns(table).ToList();
        var nullable = context.IsModern ? "?" : string.Empty;

        using (writer.Region("Fill Methods"))
        {
            WriteSummary(writer, $"Fills a {className} from the reader, starting at the given offset.");
            using (writer.Block($"protected void FillObject({baseEntity} {objectVar}, SqlDataReader reader, int start)"))
            {
                for (var i = 0; i < columns.Count; i++)
                {
                    var column = columns[i];
                    var property = NameResolver.PropertyName(column);
                    var value = SqlTypeMap.ReaderValue(column, $"start + {i}");

                    if (column.AllowDbNull)
                    {
                        writer.Line($"if (!reader.IsDBNull(start + {i})) {objectVar}.{property} = {value};");
                    }
                    else
                    {
                        writer.Line($"{objectVar}.{property} = {value};");
                    }
                }

                writer.Blank();
                writer.Line($"FillBaseObject({objectVar}, reader, (start + {columns.Count}));");
                writer.Line($"{objectVar}.RowState = BaseBusinessEntity.RowStateEnum.NormalRow;");
            }

            writer.Blank();

            WriteSummary(writer, $"Fills a {className} from the reader.");
            using (writer.Block($"protected void FillObject({baseEntity} {objectVar}, SqlDataReader reader)"))
            {
                writer.Line($"FillObject({objectVar}, reader, 0);");
            }

            writer.Blank();

            WriteSummary(writer, $"Reads a single {className} from the command, or null when there is no row.");
            using (writer.Block($"private {className}{nullable} GetObject(SqlCommand cmd)"))
            {
                writer.Line("SqlDataReader reader;");
                writer.Line("SelectRecords(cmd, out reader);");
                writer.Blank();
                using (writer.Block("using (reader)"))
                {
                    using (writer.Block("if (reader.Read())"))
                    {
                        writer.Line($"{className} {objectVar} = new {className}();");
                        writer.Line($"FillObject({objectVar}, reader);");
                        writer.Line($"return {objectVar};");
                    }

                    writer.Blank();
                    writer.Line("return null;");
                }
            }

            writer.Blank();

            WriteSummary(writer, $"Reads up to the requested number of {className} rows from the command.");
            using (writer.Block($"private {listName} GetList(SqlCommand cmd, long rows)"))
            {
                writer.Line("SqlDataReader reader;");
                writer.Line("SelectRecords(cmd, out reader);");
                writer.Blank();
                writer.Line($"{listName} list = new {listName}();");
                writer.Blank();
                using (writer.Block("using (reader)"))
                {
                    using (writer.Block("while (reader.Read() && rows-- != 0)"))
                    {
                        writer.Line($"{className} {objectVar} = new {className}();");
                        writer.Line($"FillObject({objectVar}, reader);");
                        writer.Line($"list.Add({objectVar});");
                    }

                    writer.Blank();
                    writer.Line("// Output parameters are not populated until the reader is closed.");
                    writer.Line("reader.Close();");
                }

                writer.Blank();
                writer.Line("return list;");
            }
        }
    }
}
