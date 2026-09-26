using Voot.CodeGen.Application.Abstractions;
using Voot.CodeGen.Application.Models;
using Voot.CodeGen.Domain.Common;
using Voot.CodeGen.Domain.Generation;
using Voot.CodeGen.Domain.Projects;

namespace Voot.CodeGen.Application.Services;

public enum ProjectDatabaseMode
{
    /// <summary>Point the project at a database that already exists.</summary>
    Existing = 0,

    /// <summary>Create a new, empty database on the configured server.</summary>
    New = 1
}

public sealed record ProjectSetupRequest
{
    public required string Name { get; init; }

    public string? Description { get; init; }

    public required GenerationSettings Settings { get; init; }

    public ProjectDatabaseMode DatabaseMode { get; init; }

    /// <summary>Used with <see cref="ProjectDatabaseMode.Existing"/>.</summary>
    public string? ConnectionString { get; init; }

    /// <summary>Used with <see cref="ProjectDatabaseMode.New"/>.</summary>
    public string? NewDatabaseName { get; init; }

    /// <summary>An optional script to apply as the project's first change.</summary>
    public string? Script { get; init; }

    public string? ScriptFileName { get; init; }
}

/// <param name="RunId">The run applying the uploaded script, when there was one.</param>
/// <param name="CreatedDatabase">The database created for the project, when one was.</param>
public sealed record ProjectSetupResult(Guid ProjectId, Guid? RunId, string? CreatedDatabase);

/// <summary>
/// Creates a project, either against an existing database or a new one created for it, and
/// optionally applies an uploaded script as its first change. Everything that can be checked
/// is checked before anything is created, so a rejected request leaves nothing behind.
/// </summary>
public sealed class ProjectSetupService(
    ProjectService projects,
    IProjectRepository repository,
    IDatabaseProvisioner provisioner,
    ChangeSubmissionService submissions,
    ProjectAccessService access)
{
    private const int MaxTitleLength = 200;

    public bool CanCreateDatabases => provisioner.IsConfigured;

    public string? DatabaseServerName => provisioner.ServerName;

    public async Task<ProjectSetupResult> CreateAsync(
        ProjectSetupRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        access.RequireAdministrator();

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new DomainException("The project name is required.");
        }

        if (await repository.NameExistsAsync(request.Name.Trim(), null, cancellationToken))
        {
            throw new DomainException($"A project named '{request.Name.Trim()}' already exists.");
        }

        if (request.Script is not null)
        {
            CheckScript(request.Script, request.ScriptFileName);
        }

        string connectionString;
        string? createdDatabase = null;

        if (request.DatabaseMode == ProjectDatabaseMode.Existing)
        {
            if (string.IsNullOrWhiteSpace(request.ConnectionString))
            {
                throw new DomainException("A connection string is required.");
            }

            connectionString = request.ConnectionString;
        }
        else
        {
            var databaseName = request.NewDatabaseName?.Trim();

            if (!provisioner.IsConfigured)
            {
                throw new DomainException(
                    "Creating databases is not set up. An administrator has to set DatabaseProvisioning:ServerConnectionString.");
            }

            if (DatabaseNameRule.Validate(databaseName) is { } nameError)
            {
                throw new DomainException(nameError);
            }

            if (await provisioner.DatabaseExistsAsync(databaseName!, cancellationToken))
            {
                throw new DomainException(
                    $"A database named '{databaseName}' already exists on {provisioner.ServerName}. " +
                    "To use it, choose \"Use an existing database\" and enter its connection string.");
            }

            connectionString = await provisioner.CreateDatabaseAsync(databaseName!, cancellationToken);
            createdDatabase = databaseName;
        }

        Guid projectId;

        try
        {
            projectId = await projects.CreateAsync(
                request.Name, request.Description, connectionString, request.Settings, cancellationToken);
        }
        catch (Exception ex) when (createdDatabase is not null)
        {
            // The database was created a moment ago for this project alone and is still empty.
            throw await RemoveCreatedDatabaseAsync(createdDatabase, ex);
        }

        Guid? runId = null;

        if (request.Script is not null)
        {
            // All tables: there is no earlier generation to compare against yet.
            runId = await submissions.SubmitAsync(
                projectId, request.Script, Title(request.ScriptFileName), GenerationScope.AllTables, cancellationToken);
        }

        return new ProjectSetupResult(projectId, runId, createdDatabase);
    }

    private static void CheckScript(string script, string? fileName)
    {
        var name = string.IsNullOrWhiteSpace(fileName) ? "The SQL file" : $"'{fileName}'";

        if (string.IsNullOrWhiteSpace(script))
        {
            throw new DomainException($"{name} is empty.");
        }

        var issues = SqlScriptInspector.FindDatabaseLevelStatements(script);

        if (issues.Count > 0)
        {
            var listed = string.Join("; ", issues.Take(5).Select(i => $"line {i.Line}: {i.Text}"));
            var more = issues.Count > 5 ? $" and {issues.Count - 5} more" : string.Empty;

            throw new DomainException(
                $"{name} switches to, creates or changes a database ({listed}{more}). " +
                "The script runs against the project's own database, so remove USE and " +
                "CREATE/ALTER/DROP DATABASE statements and upload it again.");
        }
    }

    private static string Title(string? fileName)
    {
        var title = string.IsNullOrWhiteSpace(fileName) ? "Initial script" : $"Initial script: {Path.GetFileName(fileName)}";

        return title.Length > MaxTitleLength ? title[..MaxTitleLength] : title;
    }

    private async Task<Exception> RemoveCreatedDatabaseAsync(string databaseName, Exception cause)
    {
        try
        {
            await provisioner.DropDatabaseAsync(databaseName, CancellationToken.None);
            return cause;
        }
        catch (Exception dropError)
        {
            return new DomainException(
                $"{cause.Message} The database '{databaseName}' created for it could not be removed " +
                $"({dropError.Message}); drop it on {provisioner.ServerName} by hand.",
                cause);
        }
    }
}
