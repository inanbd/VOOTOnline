using System.ComponentModel.DataAnnotations;

namespace Voot.CodeGen.Web.ViewModels;

public sealed class LoginViewModel
{
    [Required]
    [Display(Name = "User name")]
    public string UserName { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Display(Name = "Keep me signed in")]
    public bool RememberMe { get; set; }

    public string? ReturnUrl { get; set; }
}

public sealed class ChangePasswordViewModel
{
    [Required]
    [DataType(DataType.Password)]
    [Display(Name = "Current password")]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    [Display(Name = "New password")]
    public string NewPassword { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    [Display(Name = "Confirm new password")]
    [Compare(nameof(NewPassword), ErrorMessage = "The passwords do not match.")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

public sealed class CreateUserViewModel
{
    [Required]
    [Display(Name = "User name")]
    [StringLength(256, MinimumLength = 3)]
    public string UserName { get; set; } = string.Empty;

    [EmailAddress]
    public string? Email { get; set; }

    [Display(Name = "Display name")]
    public string? DisplayName { get; set; }

    [Required]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Display(Name = "Administrator")]
    public bool IsAdministrator { get; set; }
}
