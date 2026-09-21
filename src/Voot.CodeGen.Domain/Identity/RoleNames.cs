namespace Voot.CodeGen.Domain.Identity;

/// <summary>The two roles the application ships with.</summary>
public static class RoleNames
{
    /// <summary>Creates projects, sets connection strings, assigns users, manages accounts.</summary>
    public const string Administrator = "Administrator";

    /// <summary>Submits SQL changes and downloads archives for assigned projects only.</summary>
    public const string User = "User";

    public static readonly IReadOnlyList<string> All = [Administrator, User];
}
