using Voot.CodeGen.Application.Abstractions;
using Voot.CodeGen.Domain.Common;
using Voot.CodeGen.Domain.Projects;

namespace Voot.CodeGen.Application.Services;

/// <summary>
/// Administrator operations on projects: creation, settings, the target connection string,
/// and which users may work on them.
/// </summary>
public sealed class ProjectService(
    IProjectRepository projects,
    IConnectionStringProtector protector,
    ISchemaReader schemaReader,
    ProjectAccessService access,
    ICurrentUser currentUser,
    IClock clock)
{
    public async Task<Guid> CreateAsync(
        string name,
        string? description,
        string connectionString,
        GenerationSettings settings,
        CancellationToken cancellationToken = default)
    {
        access.RequireAdministrator();
        await GuardNameAsync(name, null, cancellationToken);

        var info = protector.Protect(connectionString);

        var project = new Project
        {
            Name = name.Trim(),
            Description = description?.Trim(),
            ProtectedConnectionString = info.Protected,
            ConnectionStringSummary = info.Summary,
            Settings = settings,
            CreatedByUserId = currentUser.UserId!,
            CreatedUtc = clock.UtcNow
        };

        await projects.AddAsync(project, cancellationToken);
        return project.Id;
    }

    /// <summary>
    /// Updates a project. A null <paramref name="connectionString"/> leaves the stored one
    /// alone, so the edit form never has to round-trip the secret.
    /// </summary>
    public async Task UpdateAsync(
        Guid id,
        string name,
        string? description,
        string? connectionString,
        GenerationSettings settings,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        access.RequireAdministrator();

        var project = await projects.GetAsync(id, cancellationToken) ?? throw new NotFoundException("Project");
        await GuardNameAsync(name, id, cancellationToken);

        project.Name = name.Trim();
        project.Description = description?.Trim();
        project.Settings = settings;
        project.IsActive = isActive;
        project.UpdatedUtc = clock.UtcNow;

        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            var info = protector.Protect(connectionString);
            project.ProtectedConnectionString = info.Protected;
            project.ConnectionStringSummary = info.Summary;
        }

        await projects.UpdateAsync(project, cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        access.RequireAdministrator();

        _ = await projects.GetAsync(id, cancellationToken) ?? throw new NotFoundException("Project");
        await projects.DeleteAsync(id, cancellationToken);
    }

    /// <summary>Opens a connection to the project's database and reports the result.</summary>
    public async Task<string?> TestConnectionAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var project = await access.RequireAccessAsync(id, cancellationToken);
        var connectionString = protector.Unprotect(project.ProtectedConnectionString);

        return await schemaReader.TestConnectionAsync(connectionString, cancellationToken);
    }

    /// <summary>Tests a connection string that has not been saved yet.</summary>
    public async Task<string?> TestConnectionStringAsync(
        string connectionString, CancellationToken cancellationToken = default)
    {
        access.RequireAdministrator();

        // Round-trip through the protector so a malformed string fails the same way it would on save.
        var info = protector.Protect(connectionString);
        return await schemaReader.TestConnectionAsync(protector.Unprotect(info.Protected), cancellationToken);
    }

    public async Task AssignUserAsync(Guid projectId, string userId, CancellationToken cancellationToken = default)
    {
        access.RequireAdministrator();

        _ = await projects.GetAsync(projectId, cancellationToken) ?? throw new NotFoundException("Project");

        if (await projects.IsMemberAsync(projectId, userId, cancellationToken))
        {
            return;
        }

        await projects.AssignUserAsync(
            new ProjectUser
            {
                ProjectId = projectId,
                UserId = userId,
                AssignedByUserId = currentUser.UserId!,
                AssignedUtc = clock.UtcNow
            },
            cancellationToken);
    }

    public async Task RemoveUserAsync(Guid projectId, string userId, CancellationToken cancellationToken = default)
    {
        access.RequireAdministrator();
        await projects.RemoveUserAsync(projectId, userId, cancellationToken);
    }

    public Task<IReadOnlyList<ProjectUser>> GetMembersAsync(
        Guid projectId, CancellationToken cancellationToken = default)
    {
        access.RequireAdministrator();
        return projects.GetMembersAsync(projectId, cancellationToken);
    }

    private async Task GuardNameAsync(string name, Guid? excludingId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("The project name is required.");
        }

        if (await projects.NameExistsAsync(name.Trim(), excludingId, cancellationToken))
        {
            throw new DomainException($"A project named '{name.Trim()}' already exists.");
        }
    }
}
