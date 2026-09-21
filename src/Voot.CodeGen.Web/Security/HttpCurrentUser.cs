using System.Security.Claims;
using Voot.CodeGen.Application.Abstractions;
using Voot.CodeGen.Domain.Identity;

namespace Voot.CodeGen.Web.Security;

/// <summary>Adapts the signed-in principal to the application layer's view of the caller.</summary>
public sealed class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal => accessor.HttpContext?.User;

    public string? UserId => Principal?.FindFirstValue(ClaimTypes.NameIdentifier);

    public string? UserName => Principal?.Identity?.Name;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated ?? false;

    public bool IsAdministrator => Principal?.IsInRole(RoleNames.Administrator) ?? false;
}
