using Voot.CodeGen.Application.Abstractions;
using Voot.CodeGen.Domain.Common;
using Voot.CodeGen.Domain.Projects;

namespace Voot.CodeGen.Application.Services;

/// <summary>
/// The single place project authorisation is decided: administrators reach every project,
/// everyone else only the ones they are assigned to.
/// </summary>
public sealed class ProjectAccessService(IProjectRepository projects, ICurrentUser currentUser)
{
    /// <summary>Loads a project the caller is allowed to see, or throws.</summary>
    /// <exception cref="NotFoundException">No such project.</exception>
    /// <exception cref="ForbiddenException">The caller is not assigned to it.</exception>
    public async Task<Project> RequireAccessAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        var project = await projects.GetAsync(projectId, cancellationToken)
            ?? throw new NotFoundException("Project");

        if (!await CanAccessAsync(projectId, cancellationToken))
        {
            throw new ForbiddenException("You are not assigned to this project.");
        }

        return project;
    }

    public async Task<bool> CanAccessAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        if (!currentUser.IsAuthenticated)
        {
            return false;
        }

        if (currentUser.IsAdministrator)
        {
            return true;
        }

        return await projects.IsMemberAsync(projectId, currentUser.UserId!, cancellationToken);
    }

    /// <summary>Throws unless the caller is an administrator.</summary>
    public void RequireAdministrator()
    {
        if (!currentUser.IsAdministrator)
        {
            throw new ForbiddenException("This action requires the Administrator role.");
        }
    }

    /// <summary>Every project the caller may see.</summary>
    public Task<IReadOnlyList<Project>> GetVisibleProjectsAsync(CancellationToken cancellationToken = default)
    {
        if (!currentUser.IsAuthenticated)
        {
            return Task.FromResult<IReadOnlyList<Project>>([]);
        }

        return currentUser.IsAdministrator
            ? projects.GetAllAsync(cancellationToken)
            : projects.GetForUserAsync(currentUser.UserId!, cancellationToken);
    }
}
