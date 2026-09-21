namespace Voot.CodeGen.Application.Abstractions;

/// <summary>A user account, as the project-assignment screens need to see it.</summary>
/// <param name="Id">Identity user id.</param>
/// <param name="UserName">Login name.</param>
/// <param name="Email">Contact address.</param>
/// <param name="DisplayName">Friendly name, falling back to the login name.</param>
/// <param name="IsAdministrator">Whether the account holds the administrator role.</param>
/// <param name="IsActive">Whether the account may sign in.</param>
public readonly record struct DirectoryUser(
    string Id,
    string? UserName,
    string? Email,
    string? DisplayName,
    bool IsAdministrator,
    bool IsActive);

/// <summary>
/// Read-only view over the Identity user store, so the application layer can list and name
/// users without depending on ASP.NET Core Identity types.
/// </summary>
public interface IUserDirectory
{
    Task<IReadOnlyList<DirectoryUser>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<DirectoryUser?> FindAsync(string userId, CancellationToken cancellationToken = default);
}
