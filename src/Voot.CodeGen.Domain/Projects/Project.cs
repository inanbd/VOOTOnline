namespace Voot.CodeGen.Domain.Projects;

/// <summary>
/// A target database plus the generator options used for it. Created and owned by
/// administrators; regular users gain access through <see cref="ProjectUser"/>.
/// </summary>
public sealed class Project
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public required string Name { get; set; }

    public string? Description { get; set; }

    /// <summary>
    /// The target database connection string, encrypted at rest. Never hand this to a
    /// view model; use <see cref="ConnectionStringSummary"/> for display.
    /// </summary>
    public required string ProtectedConnectionString { get; set; }

    /// <summary>
    /// Redacted connection string kept for display, e.g. <c>server=.;database=Foo</c> with
    /// credentials removed. Safe to render in the UI.
    /// </summary>
    public string ConnectionStringSummary { get; set; } = string.Empty;

    public GenerationSettings Settings { get; set; } = new();

    public required string CreatedByUserId { get; set; }

    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? UpdatedUtc { get; set; }

    public bool IsActive { get; set; } = true;
}
