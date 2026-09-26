using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Voot.CodeGen.Application.Abstractions;
using Voot.CodeGen.Application.Services;
using Microsoft.AspNetCore.Routing;
using Voot.CodeGen.Domain.Generation;
using Voot.CodeGen.Domain.Projects;
using Voot.CodeGen.Generation.Naming;
using Voot.CodeGen.Web.Filters;
using Voot.CodeGen.Web.Security;
using Voot.CodeGen.Web.ViewModels;

namespace Voot.CodeGen.Web.Controllers;

public sealed class ProjectsController(
    ProjectService projects,
    ProjectSetupService setup,
    ProjectAccessService access,
    RunHistoryService history,
    SchemaBrowsingService schema,
    SchemaChangeService schemaChanges,
    DeploymentTrackingService deployments,
    IUserDirectory users) : Controller
{
    /// <summary>Project overview with its change and run history.</summary>
    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        var project = await access.RequireAccessAsync(id, cancellationToken);

        return View(new ProjectDetailViewModel
        {
            Project = project,
            Runs = await history.GetRunsAsync(id, 25, cancellationToken),
            Changes = await history.GetChangesAsync(id, 25, cancellationToken),
            IsAdministrator = User.IsInRole(Domain.Identity.RoleNames.Administrator)
        });
    }

    /// <summary>
    /// Browses the current structure of the project's database. Tables are chosen with
    /// <paramref name="tables"/>, or all of them with <paramref name="all"/>. Both live in the
    /// query string so a particular view is linkable.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Schema(
        Guid id,
        [FromQuery(Name = "table")] string[]? tables,
        bool all = false,
        CancellationToken cancellationToken = default)
    {
        var selected = tables ?? [];
        var structure = await schema.GetStructureAsync(id, selected, all, cancellationToken);

        return View(await BuildSchemaViewAsync(id, structure, selected, all, cancellationToken));
    }

    /// <summary>
    /// Runs SQL against the project's database from the schema page and shows what it changed.
    /// The script is recorded in the project's change history either way; generating the code
    /// afterwards stays a separate, explicit step.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RunSql(
        Guid id,
        string sqlText,
        string? title,
        [FromForm(Name = "table")] string[]? tables,
        bool all = false,
        CancellationToken cancellationToken = default)
    {
        var selected = tables ?? [];
        var result = await this.TryDomainAsync(() => schemaChanges.ExecuteAsync(id, sqlText, title, cancellationToken));

        // A domain rule may have rejected the request before anything ran.
        if (!ModelState.IsValid)
        {
            var current = await schema.GetStructureAsync(id, selected, all, cancellationToken);
            var rejected = await BuildSchemaViewAsync(id, current, selected, all, cancellationToken);

            return View("Schema", rejected with { SqlText = sqlText, Title = title });
        }

        // Reuse the snapshot the service already read rather than querying the catalog again.
        var project = await access.RequireAccessAsync(id, cancellationToken);
        var structure = SchemaBrowsingService.BuildStructure(project, result!.Schema, selected, all);
        var model = await BuildSchemaViewAsync(id, structure, selected, all, cancellationToken);

        return View("Schema", model with
        {
            LastChange = result,
            SqlText = result.Succeeded ? null : sqlText,
            Title = result.Succeeded ? null : title
        });
    }

    private async Task<SchemaViewModel> BuildSchemaViewAsync(
        Guid id,
        Application.Services.SchemaStructure structure,
        string[] selected,
        bool all,
        CancellationToken cancellationToken) =>
        new()
        {
            Structure = structure,
            Names = new NameResolver(structure.Project.Settings),
            SelectedNames = all
                ? [.. structure.Tables.Select(t => t.Name)]
                : new HashSet<string>(selected, StringComparer.OrdinalIgnoreCase),
            Timeline = await deployments.GetTimelineAsync(id, 40, cancellationToken),
            DevPending = await deployments.GetPendingScriptAsync(id, DeploymentEnvironment.Development, cancellationToken),
            ProductionPending = await deployments.GetPendingScriptAsync(id, DeploymentEnvironment.Production, cancellationToken)
        };

    /// <summary>Marks a single change as reaching an environment, or takes it back out.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetDeployment(
        Guid id,
        Guid changeId,
        DeploymentEnvironment environment,
        bool deployed,
        [FromForm(Name = "table")] string[]? tables,
        bool all = false,
        CancellationToken cancellationToken = default)
    {
        await deployments.SetAsync(id, changeId, environment, deployed, cancellationToken);

        return RedirectToSchema(id, tables, all);
    }

    /// <summary>Marks every change still outstanding for an environment.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAllDeployed(
        Guid id,
        DeploymentEnvironment environment,
        [FromForm(Name = "table")] string[]? tables,
        bool all = false,
        CancellationToken cancellationToken = default)
    {
        var marked = await deployments.MarkAllPendingAsync(id, environment, cancellationToken);
        var label = environment == DeploymentEnvironment.Development ? "development" : "production";

        TempData["Status"] = marked == 0
            ? $"Nothing was outstanding for {label}."
            : $"Marked {marked} change(s) as reaching {label}.";

        return RedirectToSchema(id, tables, all);
    }

    /// <summary>Returns to the schema page with the table selection intact.</summary>
    private IActionResult RedirectToSchema(Guid id, string[]? tables, bool all)
    {
        if (all)
        {
            return RedirectToAction(nameof(Schema), new { id, all = true });
        }

        var route = new RouteValueDictionary { ["id"] = id };

        if (tables is { Length: > 0 })
        {
            route["table"] = tables;
        }

        return RedirectToAction(nameof(Schema), route);
    }

    /// <summary>Largest SQL file accepted when creating a project.</summary>
    public const int MaxSqlFileBytes = 20 * 1024 * 1024;

    [Authorize(Policy = AuthorizationPolicies.Administrator)]
    [HttpGet]
    public IActionResult Create() => View("Form", WithSetupOptions(new ProjectFormViewModel()));

    /// <summary>
    /// Creates a project against an existing database or a new one, and applies an uploaded
    /// SQL file as its first change. With a file, the browser goes to that run's page.
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.Administrator)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(MaxSqlFileBytes + 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxSqlFileBytes + 1024 * 1024)]
    public async Task<IActionResult> Create(ProjectFormViewModel model, CancellationToken cancellationToken)
    {
        WithSetupOptions(model);

        if (model.DatabaseMode == ProjectDatabaseMode.Existing && string.IsNullOrWhiteSpace(model.ConnectionString))
        {
            ModelState.AddModelError(nameof(model.ConnectionString), "A connection string is required.");
        }

        if (model.DatabaseMode == ProjectDatabaseMode.New &&
            DatabaseNameRule.Validate(model.NewDatabaseName?.Trim()) is { } nameError)
        {
            ModelState.AddModelError(nameof(model.NewDatabaseName), nameError);
        }

        var script = await ReadSqlFileAsync(model.SqlFile, cancellationToken);

        if (!ModelState.IsValid)
        {
            return RedisplayCreate(model);
        }

        var result = await this.TryDomainAsync(() => setup.CreateAsync(
            new ProjectSetupRequest
            {
                Name = model.Name,
                Description = model.Description,
                Settings = model.Settings,
                DatabaseMode = model.DatabaseMode,
                ConnectionString = model.ConnectionString,
                NewDatabaseName = model.NewDatabaseName?.Trim(),
                Script = script,
                ScriptFileName = model.SqlFile?.FileName
            },
            cancellationToken));

        // A domain rule may have rejected the request; the filter puts the message in ModelState.
        if (!ModelState.IsValid)
        {
            return RedisplayCreate(model);
        }

        var created = result!.CreatedDatabase is null
            ? $"Project '{model.Name}' created."
            : $"Project '{model.Name}' created with the new database '{result.CreatedDatabase}' on {setup.DatabaseServerName}.";

        if (result.RunId is { } runId)
        {
            TempData["Status"] = $"{created} Applying '{Path.GetFileName(model.SqlFile!.FileName)}' as its first change.";
            return RedirectToAction("Details", "Runs", new { id = runId });
        }

        TempData["Status"] = created;
        return RedirectToAction(nameof(Details), new { id = result.ProjectId });
    }

    /// <summary>
    /// Reads an uploaded script as text. Byte order marks are honoured, so files saved by SQL
    /// Server Management Studio as UTF-16 read correctly; anything without one is read as UTF-8.
    /// </summary>
    private async Task<string?> ReadSqlFileAsync(IFormFile? file, CancellationToken cancellationToken)
    {
        if (file is null)
        {
            return null;
        }

        const string field = nameof(ProjectFormViewModel.SqlFile);

        if (!string.Equals(Path.GetExtension(file.FileName), ".sql", StringComparison.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(field, "Upload a .sql file.");
            return null;
        }

        if (file.Length > MaxSqlFileBytes)
        {
            ModelState.AddModelError(field, $"The SQL file can be at most {MaxSqlFileBytes / (1024 * 1024)} MB.");
            return null;
        }

        using var reader = new StreamReader(
            file.OpenReadStream(), System.Text.Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var text = await reader.ReadToEndAsync(cancellationToken);

        // UTF-16 without a byte order mark reads as text full of NULs.
        if (text.Contains('\0'))
        {
            ModelState.AddModelError(field,
                "The SQL file's text encoding could not be read. Save it as UTF-8, or as Unicode with a byte order mark, and upload it again.");
            return null;
        }

        return text;
    }

    private ProjectFormViewModel WithSetupOptions(ProjectFormViewModel model)
    {
        model.CanCreateDatabases = setup.CanCreateDatabases;
        model.DatabaseServerName = setup.DatabaseServerName;
        return model;
    }

    private ViewResult RedisplayCreate(ProjectFormViewModel model)
    {
        model.RejectedSqlFileName = model.SqlFile is null ? null : Path.GetFileName(model.SqlFile.FileName);
        return View("Form", model);
    }

    [Authorize(Policy = AuthorizationPolicies.Administrator)]
    [HttpGet]
    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        var project = await access.RequireAccessAsync(id, cancellationToken);

        return View("Form", new ProjectFormViewModel
        {
            Id = project.Id,
            Name = project.Name,
            Description = project.Description,
            ConnectionStringSummary = project.ConnectionStringSummary,
            IsActive = project.IsActive,
            Settings = project.Settings
        });
    }

    [Authorize(Policy = AuthorizationPolicies.Administrator)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, ProjectFormViewModel model, CancellationToken cancellationToken)
    {
        model.Id = id;

        if (!ModelState.IsValid)
        {
            return View("Form", model);
        }

        // A blank connection string means "keep the stored one".
        await this.TryDomainAsync(() => projects.UpdateAsync(
            id, model.Name, model.Description, model.ConnectionString, model.Settings, model.IsActive, cancellationToken));

        if (!ModelState.IsValid)
        {
            return View("Form", model);
        }

        TempData["Status"] = "Project saved.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [Authorize(Policy = AuthorizationPolicies.Administrator)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await projects.DeleteAsync(id, cancellationToken);

        TempData["Status"] = "Project deleted. Its run history was kept.";
        return RedirectToAction("Index", "Home");
    }

    /// <summary>Opens a connection to the project's database and reports what happened.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TestConnection(Guid id, CancellationToken cancellationToken)
    {
        var error = await projects.TestConnectionAsync(id, cancellationToken);

        if (error is null)
        {
            TempData["Status"] = "Connected to the project database successfully.";
        }
        else
        {
            TempData["Error"] = $"Could not connect: {error}";
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    // ---- membership ----

    [Authorize(Policy = AuthorizationPolicies.Administrator)]
    [HttpGet]
    public async Task<IActionResult> Members(Guid id, CancellationToken cancellationToken)
    {
        var project = await access.RequireAccessAsync(id, cancellationToken);
        var members = await projects.GetMembersAsync(id, cancellationToken);
        var assigned = members.Select(m => m.UserId).ToHashSet(StringComparer.Ordinal);
        var all = await users.GetAllAsync(cancellationToken);

        return View(new ProjectMembersViewModel
        {
            Project = project,
            Members = members,
            Available = [.. all.Where(u => !assigned.Contains(u.Id) && u.IsActive)]
        });
    }

    [Authorize(Policy = AuthorizationPolicies.Administrator)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddMember(Guid id, string userId, CancellationToken cancellationToken)
    {
        await projects.AssignUserAsync(id, userId, cancellationToken);

        TempData["Status"] = "User assigned to the project.";
        return RedirectToAction(nameof(Members), new { id });
    }

    [Authorize(Policy = AuthorizationPolicies.Administrator)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveMember(Guid id, string userId, CancellationToken cancellationToken)
    {
        await projects.RemoveUserAsync(id, userId, cancellationToken);

        TempData["Status"] = "User removed from the project.";
        return RedirectToAction(nameof(Members), new { id });
    }
}
