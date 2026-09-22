using Voot.CodeGen.Application.Abstractions;
using Voot.CodeGen.Application.Models;
using Voot.CodeGen.Domain.Projects;
using Voot.CodeGen.Domain.Schema;

namespace Voot.CodeGen.Application.Services;

/// <summary>
/// Read-only view over a project's target database, so the current table structures can be
/// inspected without running a generation. Every call is access-checked against the project.
/// </summary>
public sealed class SchemaBrowsingService(
    ISchemaReader schemaReader,
    IConnectionStringProtector protector,
    ProjectAccessService access)
{
    /// <summary>The tables available to pick from, ordered by schema then name.</summary>
    public async Task<SchemaTableList> GetTableListAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        var project = await access.RequireAccessAsync(projectId, cancellationToken);
        var connectionString = protector.Unprotect(project.ProtectedConnectionString);

        var tables = await schemaReader.ListTablesAsync(connectionString, cancellationToken);

        return new SchemaTableList(project, tables);
    }

    /// <summary>
    /// The full structure of the requested tables. <paramref name="selectedTables"/> is matched
    /// case-insensitively against table names; passing <paramref name="selectAll"/> returns
    /// every table and ignores the selection.
    /// </summary>
    public async Task<SchemaStructure> GetStructureAsync(
        Guid projectId,
        IReadOnlyCollection<string> selectedTables,
        bool selectAll,
        CancellationToken cancellationToken = default)
    {
        var project = await access.RequireAccessAsync(projectId, cancellationToken);
        var connectionString = protector.Unprotect(project.ProtectedConnectionString);

        // One read covers both the picker and the detail; the catalog queries are the same
        // four regardless of how many tables were picked.
        var database = await schemaReader.ReadAsync(connectionString, cancellationToken);

        return BuildStructure(project, database, selectedTables, selectAll);
    }

    /// <summary>
    /// Shapes a snapshot into the browser's view model. Separate from the read so a caller that
    /// already has a snapshot — the SQL runner, which re-reads to diff — does not read again.
    /// </summary>
    public static SchemaStructure BuildStructure(
        Project project,
        DatabaseModel database,
        IReadOnlyCollection<string> selectedTables,
        bool selectAll)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(selectedTables);

        var summaries = database.Tables
            .Select(t => new TableSummary(t.Owner, t.Name, t.Columns.Count, t.HasPrimaryKey))
            .ToList();

        var chosen = SelectTables(database, selectedTables, selectAll);

        // A requested table that is no longer in the database is reported rather than ignored,
        // so a stale bookmark does not silently show less than it says.
        var present = database.Tables.Select(t => t.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var missing = selectAll
            ? []
            : selectedTables.Where(n => !present.Contains(n)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        return new SchemaStructure(project, database, summaries, chosen, missing, selectAll);
    }

    /// <summary>Resolves the requested selection against the schema. Internal so it can be tested directly.</summary>
    internal static List<TableModel> SelectTables(
        DatabaseModel database, IReadOnlyCollection<string> selectedTables, bool selectAll)
    {
        if (selectAll)
        {
            return [.. database.Tables];
        }

        if (selectedTables.Count == 0)
        {
            return [];
        }

        var wanted = selectedTables.ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Ordered by the database's order, not the order they were ticked, so the page is
        // stable however the selection was made.
        return [.. database.Tables.Where(t => wanted.Contains(t.Name))];
    }
}

/// <summary>The picker's data: the project plus every table that can be inspected.</summary>
public sealed record SchemaTableList(Project Project, IReadOnlyList<TableSummary> Tables);

/// <summary>The browser's data: the picker, plus the structure of whatever was selected.</summary>
/// <param name="Database">The whole schema snapshot, used to resolve foreign key targets.</param>
/// <param name="Tables">Every table, for the picker.</param>
/// <param name="Selected">The tables whose structure is shown.</param>
/// <param name="MissingTables">Requested names that no longer exist in the database.</param>
public sealed record SchemaStructure(
    Project Project,
    DatabaseModel Database,
    IReadOnlyList<TableSummary> Tables,
    IReadOnlyList<TableModel> Selected,
    IReadOnlyList<string> MissingTables,
    bool SelectAll);
