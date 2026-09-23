using Voot.CodeGen.Domain.Generation;
using Voot.CodeGen.Domain.Projects;
using Voot.CodeGen.Domain.Schema;
using Voot.CodeGen.Generation.Emitters;

namespace Voot.CodeGen.Generation;

/// <summary>
/// Runs every applicable emitter over every table, replacing the master <c>Voot.cst</c>
/// template. A table that fails is recorded as a diagnostic and the run continues, so one
/// bad table cannot cost the user the whole archive.
/// </summary>
public sealed class CodeGenerator : ICodeGenerator
{
    private readonly IReadOnlyList<IEmitter> _emitters;

    public CodeGenerator() : this(DefaultEmitters()) { }

    public CodeGenerator(IReadOnlyList<IEmitter> emitters) => _emitters = emitters;

    /// <summary>The eleven emitters that correspond to the original templates.</summary>
    public static IReadOnlyList<IEmitter> DefaultEmitters() =>
    [
        new BaseEntityEmitter(),
        new EntityEmitter(),
        new PartialEntityEmitter(),
        new EntityListEmitter(),
        new MainDataAccessEmitter(),
        new MainRelationDataAccessEmitter(),
        new PartialDataAccessEmitter(),
        new MainBusinessManagerEmitter(),
        new PartialBusinessManagerEmitter(),
        new StoredProcedureEmitter()
    ];

    public GenerationResult Generate(
        DatabaseModel database, GenerationSettings settings, TableSelection? selection = null)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(settings);

        var context = new GenerationContext(database, settings);
        var files = new List<GeneratedFile>();
        var diagnostics = new List<GenerationDiagnostic>();
        var generated = 0;
        var skipped = 0;
        var generatedTables = new List<string>();

        // Filtering happens here rather than on the model, so emitters still see every table.
        foreach (var table in database.Tables.Where(t => selection is null || selection.Includes(t)))
        {
            if (!table.HasPrimaryKey)
            {
                skipped++;
                diagnostics.Add(GenerationDiagnostic.Warning(
                    "Skipped: the table has no primary key, which the generated data access layer requires.",
                    table.Name));
                continue;
            }

            var unsupported = table.Columns
                .Where(c => !Naming.SqlTypeMap.IsKnown(c.NativeType))
                .ToList();

            if (unsupported.Count > 0)
            {
                skipped++;
                diagnostics.Add(GenerationDiagnostic.Error(
                    $"Skipped: unsupported column type(s) {string.Join(", ", unsupported.Select(c => $"{c.Name} ({c.NativeType})"))}.",
                    table.Name));
                continue;
            }

            generated++;
            generatedTables.Add(table.QualifiedName);

            foreach (var emitter in _emitters)
            {
                if (!emitter.AppliesTo(table, context))
                {
                    continue;
                }

                try
                {
                    files.AddRange(emitter.Emit(table, context));
                }
                catch (Exception ex)
                {
                    diagnostics.Add(GenerationDiagnostic.Error(
                        $"{emitter.Name} failed: {ex.Message}",
                        table.Name,
                        emitter.Name,
                        ex.ToString()));
                }
            }
        }

        files.Add(BuildReadme(context, files, generated, skipped, selection, generatedTables));

        return new GenerationResult
        {
            Files = files,
            Diagnostics = diagnostics,
            TableCount = generated,
            SkippedTableCount = skipped,
            IsPartial = selection is not null,
            GeneratedTables = generatedTables
        };
    }

    /// <summary>
    /// A README at the archive root. The archive deliberately ships generated files only, so
    /// this spells out the framework types the code expects to compile against.
    /// </summary>
    private static GeneratedFile BuildReadme(
        GenerationContext context,
        IReadOnlyList<GeneratedFile> files,
        int generated,
        int skipped,
        TableSelection? selection,
        IReadOnlyList<string> generatedTables)
    {
        var s = context.Settings;
        var writer = new CodeWriter("  ");

        writer.Line($"{s.RootBase} generated data layer");
        writer.Line(new string('=', $"{s.RootBase} generated data layer".Length));
        writer.Blank();
        writer.Line($"Generated : {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        writer.Line($"Database  : {context.Database.Name}");
        writer.Line($"Style     : {s.OutputStyle}");
        writer.Line($"Tables    : {generated} generated, {skipped} skipped");
        writer.Line($"Files     : {files.Count + 1}");
        writer.Line($"Scope     : {(selection is null ? "all tables" : "changed tables only")}");
        writer.Blank();

        if (selection is not null)
        {
            writer.Line("Partial archive");
            writer.Line("---------------");
            writer.Line("Only tables whose structure changed since the last generation are included.");
            writer.Line("Every other table is unchanged. Copy the files under Bases folders, the lists and");
            writer.Line("the procedure scripts over your existing ones. Take the hand-editable partials only");
            writer.Line("for tables that are new, so hand-written code is not overwritten.");
            writer.Blank();

            foreach (var table in generatedTables)
            {
                writer.Line($"  {table}");
            }

            if (selection.DroppedTables.Count > 0)
            {
                writer.Blank();
                writer.Line("Dropped since the last generation; delete their generated files:");

                foreach (var table in selection.DroppedTables)
                {
                    writer.Line($"  {table}");
                }
            }

            writer.Blank();
        }

        writer.Line("Layout");
        writer.Line("------");
        writer.Line($"{context.EntityBaseFolder}/      entity bases and entities (regenerated every run)");
        writer.Line($"{context.EntityFolder}/      hand-editable entity partials");
        writer.Line($"{context.EntityListFolder}/      typed collections");
        writer.Line($"{context.DataAccessBaseFolder}/      data access (regenerated every run)");
        writer.Line($"{context.DataAccessFolder}/      hand-editable data access partials");
        writer.Line($"{context.BusinessLogicBaseFolder}/      managers (regenerated every run)");
        writer.Line($"{context.BusinessLogicFolder}/      hand-editable manager partials");
        writer.Line($"{context.StoredProcedureFolder}/      one T-SQL script per table");
        writer.Blank();

        writer.Line("Regenerating");
        writer.Line("------------");
        writer.Line("Files under a 'Bases' folder are overwritten on every run; do not edit them.");
        writer.Line("The partial classes in the parent folders are scaffolding, emitted every run");
        writer.Line("as well. Copy those across only on the first generation, or your hand-written");
        writer.Line("code will be overwritten.");
        writer.Blank();

        writer.Line("Required framework");
        writer.Line("------------------");
        writer.Line("This archive contains generated files only. It compiles against your own");
        writer.Line($"{context.FrameworkNamespace} assembly, which must provide:");
        writer.Blank();
        writer.Line("  BaseBusinessEntity      base entity with RowState, PropertyChanging/Changed,");
        writer.Line("                          CloneBase, CustomPropertyMatch and GetObjectData");
        writer.Line("  BaseCollection<T>       base typed collection");
        writer.Line("  BaseDataAccess          a parameterless constructor, one taking a connection");
        writer.Line("                          string, and one taking the context; plus");
        writer.Line("                          GetSPCommand, AddParameter, GetOutParameter,");
        writer.Line("                          InsertRecord, UpdateRecord, DeleteRecord,");
        writer.Line("                          SelectRecords, FillBaseObject, ALL_AVAILABLE_RECORDS");
        writer.Line("                          and the p* parameter helpers (pInt32, pNVarChar, ...)");
        writer.Line("  BaseRelationData        the same surface for junction tables");
        writer.Line("  BaseManager             base business manager");
        writer.Line("  PagedRequest            PageIndex, RowPerPage, WhereClause, SortColumn,");
        writer.Line("                          SortOrder, TotalRows");
        writer.Line($"  {s.ContextName,-22} ambient context passed to every constructor");
        writer.Line("  ObjectInsertException, ObjectUpdateException, ObjectDeleteException");
        writer.Blank();
        writer.Line("The base classes are expected to supply the audit columns");
        writer.Line($"({string.Join(", ", s.AuditColumns)}) themselves: the stored procedures declare");
        writer.Line("parameters for them, and the generated C# does not pass them.");
        writer.Blank();

        writer.Line("A note on GetByQuery and GetPaged");
        writer.Line("---------------------------------");
        writer.Line("Both concatenate a caller-supplied fragment into dynamic SQL inside the stored");
        writer.Line("procedure. Sort column and direction are validated, but the WHERE fragment is");
        writer.Line("not and cannot be. Never pass a value derived from end-user input.");

        return new GeneratedFile
        {
            RelativePath = "README.txt",
            Content = writer.ToString(),
            Emitter = "Readme"
        };
    }
}
