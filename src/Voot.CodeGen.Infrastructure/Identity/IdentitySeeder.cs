using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Voot.CodeGen.Domain.Identity;

namespace Voot.CodeGen.Infrastructure.Identity;

public sealed class SeedAdministratorOptions
{
    /// <summary>Leave empty to skip seeding; the roles are still created.</summary>
    public string UserName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Only used when the account does not exist. Supply it through user secrets or an
    /// environment variable, never in a committed appsettings file.
    /// </summary>
    public string Password { get; set; } = string.Empty;
}

/// <summary>
/// Creates the two roles at startup, and the first administrator if one is configured and no
/// administrator exists yet. Never resets an existing account's password.
/// </summary>
public sealed class IdentitySeeder(
    RoleManager<ApplicationRole> roleManager,
    UserManager<ApplicationUser> userManager,
    IOptions<SeedAdministratorOptions> options,
    ILogger<IdentitySeeder> logger)
{
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        foreach (var roleName in RoleNames.All)
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                logger.LogInformation("Creating role {Role}.", roleName);
                await roleManager.CreateAsync(new ApplicationRole { Name = roleName });
            }
        }

        var seed = options.Value;

        if (string.IsNullOrWhiteSpace(seed.UserName) || string.IsNullOrWhiteSpace(seed.Password))
        {
            return;
        }

        var existingAdmins = await userManager.GetUsersInRoleAsync(RoleNames.Administrator);

        if (existingAdmins.Count > 0)
        {
            return;
        }

        var existing = await userManager.FindByNameAsync(seed.UserName);

        if (existing is not null)
        {
            // The account is already there; promote it rather than touching its password.
            logger.LogInformation("Granting the administrator role to existing user {User}.", seed.UserName);
            await userManager.AddToRoleAsync(existing, RoleNames.Administrator);
            return;
        }

        var user = new ApplicationUser
        {
            UserName = seed.UserName,
            Email = string.IsNullOrWhiteSpace(seed.Email) ? null : seed.Email,
            EmailConfirmed = true,
            DisplayName = seed.UserName
        };

        var result = await userManager.CreateAsync(user, seed.Password);

        if (!result.Succeeded)
        {
            logger.LogError(
                "Could not create the seed administrator: {Errors}",
                string.Join("; ", result.Errors.Select(e => e.Description)));
            return;
        }

        await userManager.AddToRoleAsync(user, RoleNames.Administrator);
        logger.LogInformation("Created the seed administrator {User}.", seed.UserName);
    }
}
