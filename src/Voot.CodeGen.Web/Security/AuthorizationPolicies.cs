using Microsoft.AspNetCore.Authorization;
using Voot.CodeGen.Domain.Identity;

namespace Voot.CodeGen.Web.Security;

public static class AuthorizationPolicies
{
    /// <summary>Creating projects, setting connection strings, assigning users, managing accounts.</summary>
    public const string Administrator = "Administrator";

    public static void AddApplicationPolicies(this AuthorizationOptions options)
    {
        options.AddPolicy(Administrator, policy => policy.RequireRole(RoleNames.Administrator));

        // Everything requires a signed-in user unless an action opts out with [AllowAnonymous].
        options.FallbackPolicy = new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .Build();
    }
}
