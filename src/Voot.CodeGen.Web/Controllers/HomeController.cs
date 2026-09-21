using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Voot.CodeGen.Application.Abstractions;
using Voot.CodeGen.Application.Services;
using Voot.CodeGen.Web.ViewModels;

namespace Voot.CodeGen.Web.Controllers;

public sealed class HomeController(
    ProjectAccessService access,
    IGenerationRepository repository) : Controller
{
    /// <summary>Dashboard: the projects the caller may see, each with its most recent run.</summary>
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var projects = await access.GetVisibleProjectsAsync(cancellationToken);
        var items = new List<ProjectListItemViewModel>(projects.Count);

        foreach (var project in projects)
        {
            var runs = await repository.GetRunsAsync(project.Id, take: 1, cancellationToken);
            items.Add(new ProjectListItemViewModel { Project = project, LatestRun = runs.Count > 0 ? runs[0] : null });
        }

        return View(items);
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() =>
        View(new Models.ErrorViewModel
        {
            RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier
        });
}
