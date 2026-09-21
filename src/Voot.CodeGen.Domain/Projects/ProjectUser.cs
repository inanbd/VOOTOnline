namespace Voot.CodeGen.Domain.Projects;

/// <summary>Assignment of a regular user to a project. Administrators have implicit access to all projects.</summary>
public sealed class ProjectUser
{
    public required Guid ProjectId { get; set; }

    public required string UserId { get; set; }

    /// <summary>Denormalised for display in assignment lists; the user row remains the source of truth.</summary>
    public string? UserName { get; set; }

    public string? Email { get; set; }

    public required string AssignedByUserId { get; set; }

    public DateTimeOffset AssignedUtc { get; set; } = DateTimeOffset.UtcNow;
}
