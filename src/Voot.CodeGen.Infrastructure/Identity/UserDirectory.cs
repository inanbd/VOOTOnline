using Dapper;
using Voot.CodeGen.Application.Abstractions;
using Voot.CodeGen.Domain.Identity;
using Voot.CodeGen.Infrastructure.Data;

namespace Voot.CodeGen.Infrastructure.Identity;

/// <inheritdoc />
public sealed class UserDirectory(ISqlConnectionFactory connectionFactory) : IUserDirectory
{
    private const string BaseQuery = """
        SELECT
            u.[Id],
            u.[UserName],
            u.[Email],
            u.[DisplayName],
            u.[IsActive],
            CAST(CASE WHEN EXISTS (
                SELECT 1 FROM [dbo].[AspNetUserRoles] ur
                INNER JOIN [dbo].[AspNetRoles] r ON r.[Id] = ur.[RoleId]
                WHERE ur.[UserId] = u.[Id] AND r.[NormalizedName] = @adminRole
            ) THEN 1 ELSE 0 END AS bit) AS [IsAdministrator]
        FROM [dbo].[AspNetUsers] u
        """;

    private sealed record UserRow(
        string Id, string? UserName, string? Email, string? DisplayName, bool IsActive, bool IsAdministrator);

    private static DirectoryUser Map(UserRow r) =>
        new(r.Id, r.UserName, r.Email, r.DisplayName ?? r.UserName, r.IsAdministrator, r.IsActive);

    public async Task<IReadOnlyList<DirectoryUser>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        var rows = await connection.QueryAsync<UserRow>(new CommandDefinition(
            $"{BaseQuery} ORDER BY u.[UserName];",
            new { adminRole = RoleNames.Administrator.ToUpperInvariant() },
            cancellationToken: cancellationToken));

        return [.. rows.Select(Map)];
    }

    public async Task<DirectoryUser?> FindAsync(string userId, CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        var row = await connection.QuerySingleOrDefaultAsync<UserRow>(new CommandDefinition(
            $"{BaseQuery} WHERE u.[Id] = @userId;",
            new { adminRole = RoleNames.Administrator.ToUpperInvariant(), userId },
            cancellationToken: cancellationToken));

        return row is null ? null : Map(row);
    }
}
