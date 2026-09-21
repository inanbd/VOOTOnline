using System.ComponentModel.DataAnnotations;
using Voot.CodeGen.Application.Abstractions;
using Voot.CodeGen.Domain.Generation;
using Voot.CodeGen.Domain.Projects;

namespace Voot.CodeGen.Web.ViewModels;

/// <summary>
/// Create and edit form for a project. The connection string is write-only: the stored value
/// is never sent back to the browser, only its redacted summary.
/// </summary>
public sealed class ProjectFormViewModel
{
    public Guid? Id { get; set; }

    [Required]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Description { get; set; }

    /// <summary>
    /// Leave blank when editing to keep the stored connection string.
    /// </summary>
    [Display(Name = "Connection string")]
    [DataType(DataType.Password)]
    public string? ConnectionString { get; set; }

    /// <summary>Redacted form of the stored string, for display only.</summary>
    public string? ConnectionStringSummary { get; set; }

    [Display(Name = "Active")]
    public bool IsActive { get; set; } = true;

    public GenerationSettings Settings { get; set; } = new();

    public bool IsEdit => Id.HasValue;
}

public sealed class ProjectListItemViewModel
{
    public required Project Project { get; init; }

    public GenerationRun? LatestRun { get; init; }
}

public sealed class ProjectDetailViewModel
{
    public required Project Project { get; init; }

    public required IReadOnlyList<GenerationRun> Runs { get; init; }

    public required IReadOnlyList<ChangeRequest> Changes { get; init; }

    public bool IsAdministrator { get; init; }
}

public sealed class ProjectMembersViewModel
{
    public required Project Project { get; init; }

    public required IReadOnlyList<ProjectUser> Members { get; init; }

    /// <summary>Users not yet assigned, offered in the add-member picker.</summary>
    public required IReadOnlyList<DirectoryUser> Available { get; init; }
}
