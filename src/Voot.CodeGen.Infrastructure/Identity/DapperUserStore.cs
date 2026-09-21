using System.Security.Claims;
using Dapper;
using Microsoft.AspNetCore.Identity;
using Voot.CodeGen.Domain.Identity;
using Voot.CodeGen.Infrastructure.Data;

namespace Voot.CodeGen.Infrastructure.Identity;

/// <summary>
/// ASP.NET Core Identity user store backed by Dapper. Implements the store interfaces that
/// UserManager and SignInManager resolve, so the full Identity pipeline — password hashing,
/// lockout, security stamps, roles and claims — works without Entity Framework.
/// </summary>
public sealed class DapperUserStore(ISqlConnectionFactory connectionFactory) :
    IUserStore<ApplicationUser>,
    IUserPasswordStore<ApplicationUser>,
    IUserEmailStore<ApplicationUser>,
    IUserSecurityStampStore<ApplicationUser>,
    IUserLockoutStore<ApplicationUser>,
    IUserRoleStore<ApplicationUser>,
    IUserClaimStore<ApplicationUser>,
    IUserPhoneNumberStore<ApplicationUser>,
    IUserTwoFactorStore<ApplicationUser>
{
    private const string Columns = """
        [Id], [UserName], [NormalizedUserName], [Email], [NormalizedEmail], [EmailConfirmed],
        [PasswordHash], [SecurityStamp], [ConcurrencyStamp], [PhoneNumber], [PhoneNumberConfirmed],
        [TwoFactorEnabled], [LockoutEnd], [LockoutEnabled], [AccessFailedCount],
        [DisplayName], [IsActive], [CreatedUtc]
        """;

    // ---- IUserStore -------------------------------------------------------------------

    public async Task<IdentityResult> CreateAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        await connection.ExecuteAsync(new CommandDefinition(
            $"""
            INSERT INTO [dbo].[AspNetUsers] ({Columns})
            VALUES (@Id, @UserName, @NormalizedUserName, @Email, @NormalizedEmail, @EmailConfirmed,
                    @PasswordHash, @SecurityStamp, @ConcurrencyStamp, @PhoneNumber, @PhoneNumberConfirmed,
                    @TwoFactorEnabled, @LockoutEnd, @LockoutEnabled, @AccessFailedCount,
                    @DisplayName, @IsActive, @CreatedUtc);
            """,
            user,
            cancellationToken: cancellationToken));

        return IdentityResult.Success;
    }

    public async Task<IdentityResult> UpdateAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        // The concurrency stamp is rotated on every write and checked here, so two concurrent
        // edits of the same user cannot silently overwrite one another.
        var previousStamp = user.ConcurrencyStamp;
        user.ConcurrencyStamp = Guid.NewGuid().ToString();

        var affected = await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE [dbo].[AspNetUsers] SET
                [UserName] = @UserName,
                [NormalizedUserName] = @NormalizedUserName,
                [Email] = @Email,
                [NormalizedEmail] = @NormalizedEmail,
                [EmailConfirmed] = @EmailConfirmed,
                [PasswordHash] = @PasswordHash,
                [SecurityStamp] = @SecurityStamp,
                [ConcurrencyStamp] = @ConcurrencyStamp,
                [PhoneNumber] = @PhoneNumber,
                [PhoneNumberConfirmed] = @PhoneNumberConfirmed,
                [TwoFactorEnabled] = @TwoFactorEnabled,
                [LockoutEnd] = @LockoutEnd,
                [LockoutEnabled] = @LockoutEnabled,
                [AccessFailedCount] = @AccessFailedCount,
                [DisplayName] = @DisplayName,
                [IsActive] = @IsActive
            WHERE [Id] = @Id
              AND ([ConcurrencyStamp] IS NULL OR [ConcurrencyStamp] = @PreviousStamp);
            """,
            new
            {
                user.Id, user.UserName, user.NormalizedUserName, user.Email, user.NormalizedEmail,
                user.EmailConfirmed, user.PasswordHash, user.SecurityStamp, user.ConcurrencyStamp,
                user.PhoneNumber, user.PhoneNumberConfirmed, user.TwoFactorEnabled, user.LockoutEnd,
                user.LockoutEnabled, user.AccessFailedCount, user.DisplayName, user.IsActive,
                PreviousStamp = previousStamp
            },
            cancellationToken: cancellationToken));

        return affected == 0
            ? IdentityResult.Failed(new IdentityError
            {
                Code = "ConcurrencyFailure",
                Description = "The user was modified by someone else. Reload and try again."
            })
            : IdentityResult.Success;
    }

    public async Task<IdentityResult> DeleteAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM [dbo].[AspNetUsers] WHERE [Id] = @Id;",
            new { user.Id },
            cancellationToken: cancellationToken));

        return IdentityResult.Success;
    }

    public async Task<ApplicationUser?> FindByIdAsync(string userId, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        return await connection.QuerySingleOrDefaultAsync<ApplicationUser>(new CommandDefinition(
            $"SELECT {Columns} FROM [dbo].[AspNetUsers] WHERE [Id] = @userId;",
            new { userId },
            cancellationToken: cancellationToken));
    }

    public async Task<ApplicationUser?> FindByNameAsync(string normalizedUserName, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        return await connection.QuerySingleOrDefaultAsync<ApplicationUser>(new CommandDefinition(
            $"SELECT {Columns} FROM [dbo].[AspNetUsers] WHERE [NormalizedUserName] = @normalizedUserName;",
            new { normalizedUserName },
            cancellationToken: cancellationToken));
    }

    public Task<string> GetUserIdAsync(ApplicationUser user, CancellationToken cancellationToken) =>
        Task.FromResult(user.Id);

    public Task<string?> GetUserNameAsync(ApplicationUser user, CancellationToken cancellationToken) =>
        Task.FromResult(user.UserName);

    public Task SetUserNameAsync(ApplicationUser user, string? userName, CancellationToken cancellationToken)
    {
        user.UserName = userName;
        return Task.CompletedTask;
    }

    public Task<string?> GetNormalizedUserNameAsync(ApplicationUser user, CancellationToken cancellationToken) =>
        Task.FromResult(user.NormalizedUserName);

    public Task SetNormalizedUserNameAsync(ApplicationUser user, string? normalizedName, CancellationToken cancellationToken)
    {
        user.NormalizedUserName = normalizedName;
        return Task.CompletedTask;
    }

    // ---- passwords --------------------------------------------------------------------

    public Task SetPasswordHashAsync(ApplicationUser user, string? passwordHash, CancellationToken cancellationToken)
    {
        user.PasswordHash = passwordHash;
        return Task.CompletedTask;
    }

    public Task<string?> GetPasswordHashAsync(ApplicationUser user, CancellationToken cancellationToken) =>
        Task.FromResult(user.PasswordHash);

    public Task<bool> HasPasswordAsync(ApplicationUser user, CancellationToken cancellationToken) =>
        Task.FromResult(!string.IsNullOrEmpty(user.PasswordHash));

    // ---- email ------------------------------------------------------------------------

    public Task SetEmailAsync(ApplicationUser user, string? email, CancellationToken cancellationToken)
    {
        user.Email = email;
        return Task.CompletedTask;
    }

    public Task<string?> GetEmailAsync(ApplicationUser user, CancellationToken cancellationToken) =>
        Task.FromResult(user.Email);

    public Task<bool> GetEmailConfirmedAsync(ApplicationUser user, CancellationToken cancellationToken) =>
        Task.FromResult(user.EmailConfirmed);

    public Task SetEmailConfirmedAsync(ApplicationUser user, bool confirmed, CancellationToken cancellationToken)
    {
        user.EmailConfirmed = confirmed;
        return Task.CompletedTask;
    }

    public async Task<ApplicationUser?> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        return await connection.QueryFirstOrDefaultAsync<ApplicationUser>(new CommandDefinition(
            $"SELECT {Columns} FROM [dbo].[AspNetUsers] WHERE [NormalizedEmail] = @normalizedEmail;",
            new { normalizedEmail },
            cancellationToken: cancellationToken));
    }

    public Task<string?> GetNormalizedEmailAsync(ApplicationUser user, CancellationToken cancellationToken) =>
        Task.FromResult(user.NormalizedEmail);

    public Task SetNormalizedEmailAsync(ApplicationUser user, string? normalizedEmail, CancellationToken cancellationToken)
    {
        user.NormalizedEmail = normalizedEmail;
        return Task.CompletedTask;
    }

    // ---- security stamp ---------------------------------------------------------------

    public Task SetSecurityStampAsync(ApplicationUser user, string stamp, CancellationToken cancellationToken)
    {
        user.SecurityStamp = stamp;
        return Task.CompletedTask;
    }

    public Task<string?> GetSecurityStampAsync(ApplicationUser user, CancellationToken cancellationToken) =>
        Task.FromResult(user.SecurityStamp);

    // ---- lockout ----------------------------------------------------------------------

    public Task<DateTimeOffset?> GetLockoutEndDateAsync(ApplicationUser user, CancellationToken cancellationToken) =>
        Task.FromResult(user.LockoutEnd);

    public Task SetLockoutEndDateAsync(ApplicationUser user, DateTimeOffset? lockoutEnd, CancellationToken cancellationToken)
    {
        user.LockoutEnd = lockoutEnd;
        return Task.CompletedTask;
    }

    public Task<int> IncrementAccessFailedCountAsync(ApplicationUser user, CancellationToken cancellationToken) =>
        Task.FromResult(++user.AccessFailedCount);

    public Task ResetAccessFailedCountAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        user.AccessFailedCount = 0;
        return Task.CompletedTask;
    }

    public Task<int> GetAccessFailedCountAsync(ApplicationUser user, CancellationToken cancellationToken) =>
        Task.FromResult(user.AccessFailedCount);

    public Task<bool> GetLockoutEnabledAsync(ApplicationUser user, CancellationToken cancellationToken) =>
        Task.FromResult(user.LockoutEnabled);

    public Task SetLockoutEnabledAsync(ApplicationUser user, bool enabled, CancellationToken cancellationToken)
    {
        user.LockoutEnabled = enabled;
        return Task.CompletedTask;
    }

    // ---- phone / two factor -------------------------------------------------------------

    public Task SetPhoneNumberAsync(ApplicationUser user, string? phoneNumber, CancellationToken cancellationToken)
    {
        user.PhoneNumber = phoneNumber;
        return Task.CompletedTask;
    }

    public Task<string?> GetPhoneNumberAsync(ApplicationUser user, CancellationToken cancellationToken) =>
        Task.FromResult(user.PhoneNumber);

    public Task<bool> GetPhoneNumberConfirmedAsync(ApplicationUser user, CancellationToken cancellationToken) =>
        Task.FromResult(user.PhoneNumberConfirmed);

    public Task SetPhoneNumberConfirmedAsync(ApplicationUser user, bool confirmed, CancellationToken cancellationToken)
    {
        user.PhoneNumberConfirmed = confirmed;
        return Task.CompletedTask;
    }

    public Task SetTwoFactorEnabledAsync(ApplicationUser user, bool enabled, CancellationToken cancellationToken)
    {
        user.TwoFactorEnabled = enabled;
        return Task.CompletedTask;
    }

    public Task<bool> GetTwoFactorEnabledAsync(ApplicationUser user, CancellationToken cancellationToken) =>
        Task.FromResult(user.TwoFactorEnabled);

    // ---- roles ------------------------------------------------------------------------

    public async Task AddToRoleAsync(ApplicationUser user, string roleName, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO [dbo].[AspNetUserRoles] ([UserId], [RoleId])
            SELECT @userId, r.[Id]
            FROM [dbo].[AspNetRoles] r
            WHERE r.[NormalizedName] = @roleName
              AND NOT EXISTS (
                  SELECT 1 FROM [dbo].[AspNetUserRoles] ur
                  WHERE ur.[UserId] = @userId AND ur.[RoleId] = r.[Id]);
            """,
            new { userId = user.Id, roleName = roleName.ToUpperInvariant() },
            cancellationToken: cancellationToken));
    }

    public async Task RemoveFromRoleAsync(ApplicationUser user, string roleName, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        await connection.ExecuteAsync(new CommandDefinition(
            """
            DELETE ur FROM [dbo].[AspNetUserRoles] ur
            INNER JOIN [dbo].[AspNetRoles] r ON r.[Id] = ur.[RoleId]
            WHERE ur.[UserId] = @userId AND r.[NormalizedName] = @roleName;
            """,
            new { userId = user.Id, roleName = roleName.ToUpperInvariant() },
            cancellationToken: cancellationToken));
    }

    public async Task<IList<string>> GetRolesAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        var rows = await connection.QueryAsync<string>(new CommandDefinition(
            """
            SELECT r.[Name] FROM [dbo].[AspNetRoles] r
            INNER JOIN [dbo].[AspNetUserRoles] ur ON ur.[RoleId] = r.[Id]
            WHERE ur.[UserId] = @userId;
            """,
            new { userId = user.Id },
            cancellationToken: cancellationToken));

        return [.. rows];
    }

    public async Task<bool> IsInRoleAsync(ApplicationUser user, string roleName, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        var found = await connection.ExecuteScalarAsync<int?>(new CommandDefinition(
            """
            SELECT TOP 1 1 FROM [dbo].[AspNetUserRoles] ur
            INNER JOIN [dbo].[AspNetRoles] r ON r.[Id] = ur.[RoleId]
            WHERE ur.[UserId] = @userId AND r.[NormalizedName] = @roleName;
            """,
            new { userId = user.Id, roleName = roleName.ToUpperInvariant() },
            cancellationToken: cancellationToken));

        return found is not null;
    }

    public async Task<IList<ApplicationUser>> GetUsersInRoleAsync(string roleName, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        var rows = await connection.QueryAsync<ApplicationUser>(new CommandDefinition(
            $"""
            SELECT {Columns} FROM [dbo].[AspNetUsers] u
            INNER JOIN [dbo].[AspNetUserRoles] ur ON ur.[UserId] = u.[Id]
            INNER JOIN [dbo].[AspNetRoles] r ON r.[Id] = ur.[RoleId]
            WHERE r.[NormalizedName] = @roleName;
            """,
            new { roleName = roleName.ToUpperInvariant() },
            cancellationToken: cancellationToken));

        return [.. rows];
    }

    // ---- claims -----------------------------------------------------------------------

    public async Task<IList<Claim>> GetClaimsAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        var rows = await connection.QueryAsync<ClaimRow>(new CommandDefinition(
            "SELECT [ClaimType], [ClaimValue] FROM [dbo].[AspNetUserClaims] WHERE [UserId] = @userId;",
            new { userId = user.Id },
            cancellationToken: cancellationToken));

        return [.. rows.Select(r => new Claim(r.ClaimType, r.ClaimValue))];
    }

    public async Task AddClaimsAsync(ApplicationUser user, IEnumerable<Claim> claims, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO [dbo].[AspNetUserClaims] ([UserId], [ClaimType], [ClaimValue])
            VALUES (@UserId, @ClaimType, @ClaimValue);
            """,
            claims.Select(c => new { UserId = user.Id, ClaimType = c.Type, ClaimValue = c.Value }),
            cancellationToken: cancellationToken));
    }

    public async Task ReplaceClaimAsync(
        ApplicationUser user, Claim claim, Claim newClaim, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE [dbo].[AspNetUserClaims]
            SET [ClaimType] = @newType, [ClaimValue] = @newValue
            WHERE [UserId] = @userId AND [ClaimType] = @oldType AND [ClaimValue] = @oldValue;
            """,
            new
            {
                userId = user.Id,
                oldType = claim.Type, oldValue = claim.Value,
                newType = newClaim.Type, newValue = newClaim.Value
            },
            cancellationToken: cancellationToken));
    }

    public async Task RemoveClaimsAsync(ApplicationUser user, IEnumerable<Claim> claims, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        await connection.ExecuteAsync(new CommandDefinition(
            """
            DELETE FROM [dbo].[AspNetUserClaims]
            WHERE [UserId] = @UserId AND [ClaimType] = @ClaimType AND [ClaimValue] = @ClaimValue;
            """,
            claims.Select(c => new { UserId = user.Id, ClaimType = c.Type, ClaimValue = c.Value }),
            cancellationToken: cancellationToken));
    }

    public async Task<IList<ApplicationUser>> GetUsersForClaimAsync(Claim claim, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        var rows = await connection.QueryAsync<ApplicationUser>(new CommandDefinition(
            $"""
            SELECT {Columns} FROM [dbo].[AspNetUsers] u
            INNER JOIN [dbo].[AspNetUserClaims] uc ON uc.[UserId] = u.[Id]
            WHERE uc.[ClaimType] = @claimType AND uc.[ClaimValue] = @claimValue;
            """,
            new { claimType = claim.Type, claimValue = claim.Value },
            cancellationToken: cancellationToken));

        return [.. rows];
    }

    /// <summary>Dapper maps by column name, which rules out a ValueTuple here.</summary>
    private sealed record ClaimRow(string ClaimType, string ClaimValue);

    public void Dispose()
    {
        // Connections are opened per call and disposed there; nothing is held.
    }
}
