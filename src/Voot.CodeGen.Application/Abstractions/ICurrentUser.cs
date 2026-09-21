namespace Voot.CodeGen.Application.Abstractions;

/// <summary>The signed-in user, as seen by the application layer.</summary>
public interface ICurrentUser
{
    /// <summary>Identity user id, or null when the request is unauthenticated.</summary>
    string? UserId { get; }

    string? UserName { get; }

    bool IsAuthenticated { get; }

    bool IsAdministrator { get; }
}
