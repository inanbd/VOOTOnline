using Dapper;
using Microsoft.AspNetCore.Identity;
using Voot.CodeGen.Domain.Identity;
using Voot.CodeGen.Infrastructure.Data;

namespace Voot.CodeGen.Infrastructure.Identity;

/// <summary>ASP.NET Core Identity role store backed by Dapper.</summary>
public sealed class DapperRoleStore(ISqlConnectionFactory connectionFactory) : IRoleStore<ApplicationRole>
{
    public async Task<IdentityResult> CreateAsync(ApplicationRole role, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO [dbo].[AspNetRoles] ([Id], [Name], [NormalizedName], [ConcurrencyStamp])
            VALUES (@Id, @Name, @NormalizedName, @ConcurrencyStamp);
            """,
            role,
            cancellationToken: cancellationToken));

        return IdentityResult.Success;
    }

    public async Task<IdentityResult> UpdateAsync(ApplicationRole role, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        role.ConcurrencyStamp = Guid.NewGuid().ToString();

        await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE [dbo].[AspNetRoles]
            SET [Name] = @Name, [NormalizedName] = @NormalizedName, [ConcurrencyStamp] = @ConcurrencyStamp
            WHERE [Id] = @Id;
            """,
            role,
            cancellationToken: cancellationToken));

        return IdentityResult.Success;
    }

    public async Task<IdentityResult> DeleteAsync(ApplicationRole role, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM [dbo].[AspNetRoles] WHERE [Id] = @Id;",
            new { role.Id },
            cancellationToken: cancellationToken));

        return IdentityResult.Success;
    }

    public async Task<ApplicationRole?> FindByIdAsync(string roleId, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        return await connection.QuerySingleOrDefaultAsync<ApplicationRole>(new CommandDefinition(
            "SELECT [Id], [Name], [NormalizedName], [ConcurrencyStamp] FROM [dbo].[AspNetRoles] WHERE [Id] = @roleId;",
            new { roleId },
            cancellationToken: cancellationToken));
    }

    public async Task<ApplicationRole?> FindByNameAsync(string normalizedRoleName, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        return await connection.QuerySingleOrDefaultAsync<ApplicationRole>(new CommandDefinition(
            """
            SELECT [Id], [Name], [NormalizedName], [ConcurrencyStamp]
            FROM [dbo].[AspNetRoles] WHERE [NormalizedName] = @normalizedRoleName;
            """,
            new { normalizedRoleName },
            cancellationToken: cancellationToken));
    }

    public Task<string> GetRoleIdAsync(ApplicationRole role, CancellationToken cancellationToken) =>
        Task.FromResult(role.Id);

    public Task<string?> GetRoleNameAsync(ApplicationRole role, CancellationToken cancellationToken) =>
        Task.FromResult(role.Name);

    public Task SetRoleNameAsync(ApplicationRole role, string? roleName, CancellationToken cancellationToken)
    {
        role.Name = roleName;
        return Task.CompletedTask;
    }

    public Task<string?> GetNormalizedRoleNameAsync(ApplicationRole role, CancellationToken cancellationToken) =>
        Task.FromResult(role.NormalizedName);

    public Task SetNormalizedRoleNameAsync(ApplicationRole role, string? normalizedName, CancellationToken cancellationToken)
    {
        role.NormalizedName = normalizedName;
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        // Connections are opened per call and disposed there; nothing is held.
    }
}
