using Microsoft.AspNetCore.Mvc;
using Voot.CodeGen.Application.Services;
using Voot.CodeGen.Domain.Generation;
using Voot.CodeGen.Web.ViewModels;

namespace Voot.CodeGen.Web.Controllers;

public sealed class RunsController(
    ChangeSubmissionService submissions,
    RunHistoryService history,
    ArtifactDownloadService downloads,
    ProjectAccessService access) : Controller
{
    /// <summary>The SQL editor for a project.</summary>
    [HttpGet]
    public async Task<IActionResult> Submit(Guid projectId, CancellationToken cancellationToken)
    {
        var project = await access.RequireAccessAsync(projectId, cancellationToken);

        return View(new SubmitChangeViewModel
        {
            ProjectId = project.Id,
            ProjectName = project.Name,
            ConnectionStringSummary = project.ConnectionStringSummary,
            UsesTransaction = project.Settings.UseTransactionForSql,
            Scope = project.Settings.DefaultGenerationScope
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Submit(SubmitChangeViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return await RedisplaySubmitAsync(model, cancellationToken);
        }

        var runId = await submissions.SubmitAsync(
            model.ProjectId, model.SqlText, model.Title, model.Scope, cancellationToken);

        if (!ModelState.IsValid)
        {
            return await RedisplaySubmitAsync(model, cancellationToken);
        }

        return RedirectToAction(nameof(Details), new { id = runId });
    }

    /// <summary>
    /// Regenerates from the current schema without applying any SQL. With no scope posted,
    /// the project's default applies.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Regenerate(
        Guid projectId, GenerationScope? scope, CancellationToken cancellationToken)
    {
        var runId = await submissions.RegenerateAsync(projectId, scope, cancellationToken);

        return RedirectToAction(nameof(Details), new { id = runId });
    }

    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        var detail = await history.GetRunDetailAsync(id, cancellationToken);
        var project = await access.RequireAccessAsync(detail.Run.ProjectId, cancellationToken);

        return View(new RunDetailViewModel { Detail = detail, Project = project });
    }

    /// <summary>Polled by the run page while a run is in flight.</summary>
    [HttpGet]
    public async Task<IActionResult> Status(Guid id, CancellationToken cancellationToken) =>
        Json(await history.GetStatusAsync(id, cancellationToken));

    /// <summary>Streams the generated archive back to the browser.</summary>
    [HttpGet]
    public async Task<IActionResult> Download(Guid id, CancellationToken cancellationToken)
    {
        var (content, fileName) = await downloads.OpenAsync(id, cancellationToken);

        return File(content, "application/zip", fileName);
    }

    private async Task<IActionResult> RedisplaySubmitAsync(
        SubmitChangeViewModel model, CancellationToken cancellationToken)
    {
        var project = await access.RequireAccessAsync(model.ProjectId, cancellationToken);

        model.ProjectName = project.Name;
        model.ConnectionStringSummary = project.ConnectionStringSummary;
        model.UsesTransaction = project.Settings.UseTransactionForSql;

        return View("Submit", model);
    }
}
