namespace Voot.CodeGen.Domain.Identity;

/// <summary>
/// Identity user row. Shaped to match the ASP.NET Core Identity store contracts, but persisted
/// by hand-written Dapper stores rather than Entity Framework.
/// </summary>
public sealed class ApplicationUser
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public string? UserName { get; set; }

    public string? NormalizedUserName { get; set; }

    public string? Email { get; set; }

    public string? NormalizedEmail { get; set; }

    public bool EmailConfirmed { get; set; }

    public string? PasswordHash { get; set; }

    public string? SecurityStamp { get; set; }

    public string? ConcurrencyStamp { get; set; } = Guid.NewGuid().ToString();

    public string? PhoneNumber { get; set; }

    public bool PhoneNumberConfirmed { get; set; }

    public bool TwoFactorEnabled { get; set; }

    public DateTimeOffset? LockoutEnd { get; set; }

    public bool LockoutEnabled { get; set; } = true;

    public int AccessFailedCount { get; set; }

    // ---- Application-specific columns ----

    public string? DisplayName { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;
}
