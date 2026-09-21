using Voot.CodeGen.Domain.Projects;

namespace Voot.CodeGen.Application.Abstractions;

public interface IProjectRepository
{
    Task<Project?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Project>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Projects the user is assigned to. Administrators see everything instead.</summary>
    Task<IReadOnlyList<Project>> GetForUserAsync(string userId, CancellationToken cancellationToken = default);

    Task<bool> NameExistsAsync(string name, Guid? excludingId = null, CancellationToken cancellationToken = default);

    Task AddAsync(Project project, CancellationToken cancellationToken = default);

    Task UpdateAsync(Project project, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    // ---- assignments ----

    Task<IReadOnlyList<ProjectUser>> GetMembersAsync(Guid projectId, CancellationToken cancellationToken = default);

    Task<bool> IsMemberAsync(Guid projectId, string userId, CancellationToken cancellationToken = default);

    Task AssignUserAsync(ProjectUser assignment, CancellationToken cancellationToken = default);

    Task RemoveUserAsync(Guid projectId, string userId, CancellationToken cancellationToken = default);
}
