using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Voot.CodeGen.Application.Abstractions;
using Voot.CodeGen.Application.Services;
using Voot.CodeGen.Domain.Projects;
using Voot.CodeGen.Generation.Naming;
using Voot.CodeGen.Web.Security;
using Voot.CodeGen.Web.ViewModels;

namespace Voot.CodeGen.Web.Controllers;

public sealed class ProjectsController(
    ProjectService projects,
    ProjectAccessService access,
    RunHistoryService history,
    SchemaBrowsingService schema,
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

        return View(new SchemaViewModel
        {
            Structure = structure,
            Names = new NameResolver(structure.Project.Settings),
            SelectedNames = all
                ? [.. structure.Tables.Select(t => t.Name)]
                : new HashSet<string>(selected, StringComparer.OrdinalIgnoreCase)
        });
    }

    [Authorize(Policy = AuthorizationPolicies.Administrator)]
    [HttpGet]
    public IActionResult Create() => View("Form", new ProjectFormViewModel());

    [Authorize(Policy = AuthorizationPolicies.Administrator)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ProjectFormViewModel model, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(model.ConnectionString))
        {
            ModelState.AddModelError(nameof(model.ConnectionString), "A connection string is required.");
        }

        if (!ModelState.IsValid)
        {
            return View("Form", model);
        }

        var id = await projects.CreateAsync(
            model.Name, model.Description, model.ConnectionString!, model.Settings, cancellationToken);

        // A domain rule may have rejected the request; the filter puts the message in ModelState.
        if (!ModelState.IsValid)
        {
            return View("Form", model);
        }

        TempData["Status"] = $"Project '{model.Name}' created.";
        return RedirectToAction(nameof(Details), new { id });
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
        await projects.UpdateAsync(
            id, model.Name, model.Description, model.ConnectionString, model.Settings, model.IsActive, cancellationToken);

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
