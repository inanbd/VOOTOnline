using System.Data.Common;
using System.Text.Json;
using Dapper;
using Voot.CodeGen.Application.Abstractions;
using Voot.CodeGen.Domain.Projects;
using Voot.CodeGen.Infrastructure.Data;

namespace Voot.CodeGen.Infrastructure.Repositories;

/// <inheritdoc />
public sealed class ProjectRepository(ISqlConnectionFactory connectionFactory) : IProjectRepository
{
    private static readonly JsonSerializerOptions SettingsJson = new(JsonSerializerDefaults.Web);

    private const string SelectColumns = """
        [Id], [Name], [Description], [ProtectedConnectionString], [ConnectionStringSummary],
        [SettingsJson], [CreatedByUserId], [CreatedUtc], [UpdatedUtc], [IsActive]
        """;

    /// <summary>The same list qualified for the query that joins the assignment table.</summary>
    private const string ProjectColumns = """
        p.[Id], p.[Name], p.[Description], p.[ProtectedConnectionString], p.[ConnectionStringSummary],
        p.[SettingsJson], p.[CreatedByUserId], p.[CreatedUtc], p.[UpdatedUtc], p.[IsActive]
        """;

    /// <summary>The row shape; SettingsJson is expanded into <see cref="GenerationSettings"/> on read.</summary>
    private sealed record ProjectRow(
        Guid Id, string Name, string? Description, string ProtectedConnectionString,
        string ConnectionStringSummary, string SettingsJson, string CreatedByUserId,
        DateTimeOffset CreatedUtc, DateTimeOffset? UpdatedUtc, bool IsActive);

    private static Project Map(ProjectRow row) => new()
    {
        Id = row.Id,
        Name = row.Name,
        Description = row.Description,
        ProtectedConnectionString = row.ProtectedConnectionString,
        ConnectionStringSummary = row.ConnectionStringSummary,
        Settings = Deserialize(row.SettingsJson),
        CreatedByUserId = row.CreatedByUserId,
        CreatedUtc = row.CreatedUtc,
        UpdatedUtc = row.UpdatedUtc,
        IsActive = row.IsActive
    };

    /// <summary>
    /// Falls back to defaults rather than throwing: a settings blob written by an older build
    /// must not make the project unopenable.
    /// </summary>
    private static GenerationSettings Deserialize(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new GenerationSettings();
        }

        try
        {
            return JsonSerializer.Deserialize<GenerationSettings>(json, SettingsJson) ?? new GenerationSettings();
        }
        catch (JsonException)
        {
            return new GenerationSettings();
        }
    }

    public async Task<Project?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        var row = await connection.QuerySingleOrDefaultAsync<ProjectRow>(new CommandDefinition(
            $"SELECT {SelectColumns} FROM [dbo].[Projects] WHERE [Id] = @id;",
            new { id },
            cancellationToken: cancellationToken));

        return row is null ? null : Map(row);
    }

    public async Task<IReadOnlyList<Project>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        var rows = await connection.QueryAsync<ProjectRow>(new CommandDefinition(
            $"SELECT {SelectColumns} FROM [dbo].[Projects] ORDER BY [Name];",
            cancellationToken: cancellationToken));

        return [.. rows.Select(Map)];
    }

    public async Task<IReadOnlyList<Project>> GetForUserAsync(string userId, CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        var rows = await connection.QueryAsync<ProjectRow>(new CommandDefinition(
            $"""
            SELECT {ProjectColumns}
            FROM [dbo].[Projects] p
            INNER JOIN [dbo].[ProjectUsers] pu ON pu.[ProjectId] = p.[Id]
            WHERE pu.[UserId] = @userId
            ORDER BY p.[Name];
            """,
            new { userId },
            cancellationToken: cancellationToken));

        return [.. rows.Select(Map)];
    }

    public async Task<bool> NameExistsAsync(
        string name, Guid? excludingId = null, CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        var found = await connection.ExecuteScalarAsync<int?>(new CommandDefinition(
            """
            SELECT TOP 1 1 FROM [dbo].[Projects]
            WHERE [Name] = @name AND (@excludingId IS NULL OR [Id] <> @excludingId);
            """,
            new { name, excludingId },
            cancellationToken: cancellationToken));

        return found is not null;
    }

    public async Task AddAsync(Project project, CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO [dbo].[Projects]
                ([Id], [Name], [Description], [ProtectedConnectionString], [ConnectionStringSummary],
                 [SettingsJson], [CreatedByUserId], [CreatedUtc], [UpdatedUtc], [IsActive])
            VALUES
                (@Id, @Name, @Description, @ProtectedConnectionString, @ConnectionStringSummary,
                 @SettingsJson, @CreatedByUserId, @CreatedUtc, @UpdatedUtc, @IsActive);
            """,
            ToParameters(project),
            cancellationToken: cancellationToken));
    }

    public async Task UpdateAsync(Project project, CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE [dbo].[Projects] SET
                [Name] = @Name,
                [Description] = @Description,
                [ProtectedConnectionString] = @ProtectedConnectionString,
                [ConnectionStringSummary] = @ConnectionStringSummary,
                [SettingsJson] = @SettingsJson,
                [UpdatedUtc] = @UpdatedUtc,
                [IsActive] = @IsActive
            WHERE [Id] = @Id;
            """,
            ToParameters(project),
            cancellationToken: cancellationToken));
    }

    private static object ToParameters(Project project) => new
    {
        project.Id,
        project.Name,
        project.Description,
        project.ProtectedConnectionString,
        project.ConnectionStringSummary,
        SettingsJson = JsonSerializer.Serialize(project.Settings, SettingsJson),
        project.CreatedByUserId,
        project.CreatedUtc,
        project.UpdatedUtc,
        project.IsActive
    };

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        // Runs and their logs outlive the project on purpose: the audit trail is the point.
        // Only the assignment rows go, via the cascade on ProjectUsers.
        await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM [dbo].[Projects] WHERE [Id] = @id;",
            new { id },
            cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<ProjectUser>> GetMembersAsync(
        Guid projectId, CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        var rows = await connection.QueryAsync<ProjectUser>(new CommandDefinition(
            """
            SELECT pu.[ProjectId], pu.[UserId], pu.[AssignedByUserId], pu.[AssignedUtc],
                   u.[UserName], u.[Email]
            FROM [dbo].[ProjectUsers] pu
            INNER JOIN [dbo].[AspNetUsers] u ON u.[Id] = pu.[UserId]
            WHERE pu.[ProjectId] = @projectId
            ORDER BY u.[UserName];
            """,
            new { projectId },
            cancellationToken: cancellationToken));

        return [.. rows];
    }

    public async Task<bool> IsMemberAsync(Guid projectId, string userId, CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        var found = await connection.ExecuteScalarAsync<int?>(new CommandDefinition(
            "SELECT TOP 1 1 FROM [dbo].[ProjectUsers] WHERE [ProjectId] = @projectId AND [UserId] = @userId;",
            new { projectId, userId },
            cancellationToken: cancellationToken));

        return found is not null;
    }

    public async Task AssignUserAsync(ProjectUser assignment, CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        await connection.ExecuteAsync(new CommandDefinition(
            """
            IF NOT EXISTS (SELECT 1 FROM [dbo].[ProjectUsers] WHERE [ProjectId] = @ProjectId AND [UserId] = @UserId)
                INSERT INTO [dbo].[ProjectUsers] ([ProjectId], [UserId], [AssignedByUserId], [AssignedUtc])
                VALUES (@ProjectId, @UserId, @AssignedByUserId, @AssignedUtc);
            """,
            new { assignment.ProjectId, assignment.UserId, assignment.AssignedByUserId, assignment.AssignedUtc },
            cancellationToken: cancellationToken));
    }

    public async Task RemoveUserAsync(Guid projectId, string userId, CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM [dbo].[ProjectUsers] WHERE [ProjectId] = @projectId AND [UserId] = @userId;",
            new { projectId, userId },
            cancellationToken: cancellationToken));
    }
}
