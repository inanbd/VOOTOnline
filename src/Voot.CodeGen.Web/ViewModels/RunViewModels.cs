using System.ComponentModel.DataAnnotations;
using Voot.CodeGen.Application.Services;
using Voot.CodeGen.Domain.Generation;
using Voot.CodeGen.Domain.Projects;

namespace Voot.CodeGen.Web.ViewModels;

public sealed class SubmitChangeViewModel
{
    public Guid ProjectId { get; set; }

    public string? ProjectName { get; set; }

    [StringLength(200)]
    [Display(Name = "Title (optional)")]
    public string? Title { get; set; }

    [Required(ErrorMessage = "Enter the SQL to apply.")]
    [Display(Name = "SQL")]
    public string SqlText { get; set; } = string.Empty;

    /// <summary>Shown beside the editor so it is clear which database will be changed.</summary>
    public string? ConnectionStringSummary { get; set; }

    public bool UsesTransaction { get; set; } = true;

    /// <summary>Which tables to generate once the SQL succeeds; pre-set from the project default.</summary>
    public GenerationScope Scope { get; set; }
}

public sealed class RunDetailViewModel
{
    public required RunDetail Detail { get; init; }

    public required Project Project { get; init; }
}
