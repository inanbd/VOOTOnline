using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Voot.CodeGen.Application.Abstractions;
using Voot.CodeGen.Domain.Identity;
using Voot.CodeGen.Web.Security;
using Voot.CodeGen.Web.ViewModels;

namespace Voot.CodeGen.Web.Controllers;

/// <summary>Account administration. Every action here requires the administrator role.</summary>
[Authorize(Policy = AuthorizationPolicies.Administrator)]
public sealed class UsersController(
    UserManager<ApplicationUser> userManager,
    IUserDirectory directory,
    ILogger<UsersController> logger) : Controller
{
    public async Task<IActionResult> Index(CancellationToken cancellationToken) =>
        View(await directory.GetAllAsync(cancellationToken));

    [HttpGet]
    public IActionResult Create() => View(new CreateUserViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateUserViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = new ApplicationUser
        {
            UserName = model.UserName.Trim(),
            Email = string.IsNullOrWhiteSpace(model.Email) ? null : model.Email.Trim(),
            EmailConfirmed = true,
            DisplayName = string.IsNullOrWhiteSpace(model.DisplayName) ? model.UserName.Trim() : model.DisplayName.Trim()
        };

        var result = await userManager.CreateAsync(user, model.Password);

        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return View(model);
        }

        await userManager.AddToRoleAsync(user, model.IsAdministrator ? RoleNames.Administrator : RoleNames.User);

        logger.LogInformation("Created user {User}.", user.UserName);
        TempData["Status"] = $"User '{user.UserName}' created.";

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Enables or disables an account. The last remaining administrator cannot be disabled,
    /// which would otherwise lock everyone out of project administration.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleActive(string id, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(id);

        if (user is null)
        {
            return NotFound();
        }

        if (user.IsActive && await IsLastAdministratorAsync(user))
        {
            TempData["Error"] = "This is the only administrator; promote another account first.";
            return RedirectToAction(nameof(Index));
        }

        user.IsActive = !user.IsActive;
        await userManager.UpdateAsync(user);

        // Rotating the stamp invalidates the disabled user's existing cookie on its next check.
        await userManager.UpdateSecurityStampAsync(user);

        TempData["Status"] = $"User '{user.UserName}' {(user.IsActive ? "enabled" : "disabled")}.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleAdministrator(string id, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(id);

        if (user is null)
        {
            return NotFound();
        }

        var isAdmin = await userManager.IsInRoleAsync(user, RoleNames.Administrator);

        if (isAdmin && await IsLastAdministratorAsync(user))
        {
            TempData["Error"] = "This is the only administrator; promote another account first.";
            return RedirectToAction(nameof(Index));
        }

        if (isAdmin)
        {
            await userManager.RemoveFromRoleAsync(user, RoleNames.Administrator);
            await userManager.AddToRoleAsync(user, RoleNames.User);
        }
        else
        {
            await userManager.AddToRoleAsync(user, RoleNames.Administrator);
        }

        await userManager.UpdateSecurityStampAsync(user);

        TempData["Status"] = $"User '{user.UserName}' {(isAdmin ? "is no longer" : "is now")} an administrator.";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>Sets a new password for another account, for password resets.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(string id, string newPassword)
    {
        var user = await userManager.FindByIdAsync(id);

        if (user is null)
        {
            return NotFound();
        }

        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        var result = await userManager.ResetPasswordAsync(user, token, newPassword);

        if (!result.Succeeded)
        {
            TempData["Error"] = string.Join(" ", result.Errors.Select(e => e.Description));
        }
        else
        {
            logger.LogInformation("Password reset for {User} by an administrator.", user.UserName);
            TempData["Status"] = $"Password reset for '{user.UserName}'.";
        }

        return RedirectToAction(nameof(Index));
    }

    private async Task<bool> IsLastAdministratorAsync(ApplicationUser user)
    {
        if (!await userManager.IsInRoleAsync(user, RoleNames.Administrator))
        {
            return false;
        }

        var admins = await userManager.GetUsersInRoleAsync(RoleNames.Administrator);

        return admins.Count(a => a.IsActive) <= 1;
    }
}
