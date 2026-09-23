using System.IO.Compression;
using System.Text.Json;
using Dapper;
using Voot.CodeGen.Application.Models;
using Voot.CodeGen.Application.Abstractions;
using Voot.CodeGen.Domain.Generation;
using Voot.CodeGen.Infrastructure.Data;

namespace Voot.CodeGen.Infrastructure.Repositories;

/// <inheritdoc />
public sealed class GenerationRepository(ISqlConnectionFactory connectionFactory) : IGenerationRepository
{
    // ---- change requests --------------------------------------------------------------

    public async Task AddChangeRequestAsync(ChangeRequest request, CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO [dbo].[ChangeRequests]
                ([Id], [ProjectId], [SubmittedByUserId], [SubmittedByUserName], [Title], [SqlText],
                 [Status], [SubmittedUtc], [AppliedUtc], [BatchesExecuted], [RowsAffected],
                 [ErrorMessage], [ErrorNumber], [ErrorLineNumber], [StructureSummary],
                 [DeployedToDevUtc], [DeployedToDevByUserName],
                 [DeployedToProductionUtc], [DeployedToProductionByUserName])
            VALUES
                (@Id, @ProjectId, @SubmittedByUserId, @SubmittedByUserName, @Title, @SqlText,
                 @Status, @SubmittedUtc, @AppliedUtc, @BatchesExecuted, @RowsAffected,
                 @ErrorMessage, @ErrorNumber, @ErrorLineNumber, @StructureSummary,
                 @DeployedToDevUtc, @DeployedToDevByUserName,
                 @DeployedToProductionUtc, @DeployedToProductionByUserName);
            """,
            request,
            cancellationToken: cancellationToken));
    }

    public async Task UpdateChangeRequestAsync(ChangeRequest request, CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE [dbo].[ChangeRequests] SET
                [Status] = @Status,
                [AppliedUtc] = @AppliedUtc,
                [BatchesExecuted] = @BatchesExecuted,
                [RowsAffected] = @RowsAffected,
                [ErrorMessage] = @ErrorMessage,
                [ErrorNumber] = @ErrorNumber,
                [ErrorLineNumber] = @ErrorLineNumber,
                [StructureSummary] = @StructureSummary
            WHERE [Id] = @Id;
            """,
            request,
            cancellationToken: cancellationToken));
    }

    public async Task<ChangeRequest?> GetChangeRequestAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        return await connection.QuerySingleOrDefaultAsync<ChangeRequest>(new CommandDefinition(
            "SELECT * FROM [dbo].[ChangeRequests] WHERE [Id] = @id;",
            new { id },
            cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<ChangeRequest>> GetChangeRequestsAsync(
        Guid projectId, int take = 50, CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        var rows = await connection.QueryAsync<ChangeRequest>(new CommandDefinition(
            """
            SELECT TOP (@take) * FROM [dbo].[ChangeRequests]
            WHERE [ProjectId] = @projectId
            ORDER BY [SubmittedUtc] DESC;
            """,
            new { projectId, take },
            cancellationToken: cancellationToken));

        return [.. rows];
    }

    public async Task SetDeploymentAsync(
        Guid changeRequestId,
        DeploymentEnvironment environment,
        bool deployed,
        string userId,
        string? userName,
        DateTimeOffset markedUtc,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            // The column pair is chosen here rather than interpolated, so the environment value
            // can never reach the statement text.
            var sql = environment == DeploymentEnvironment.Development
                ? """
                  UPDATE [dbo].[ChangeRequests]
                  SET [DeployedToDevUtc] = @utc, [DeployedToDevByUserName] = @userName
                  WHERE [Id] = @changeRequestId;
                  """
                : """
                  UPDATE [dbo].[ChangeRequests]
                  SET [DeployedToProductionUtc] = @utc, [DeployedToProductionByUserName] = @userName
                  WHERE [Id] = @changeRequestId;
                  """;

            await connection.ExecuteAsync(new CommandDefinition(
                sql,
                new
                {
                    changeRequestId,
                    utc = deployed ? markedUtc : (DateTimeOffset?)null,
                    userName = deployed ? userName : null
                },
                transaction,
                cancellationToken: cancellationToken));

            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO [dbo].[ChangeDeployments]
                    ([ChangeRequestId], [ProjectId], [Environment], [Action],
                     [MarkedByUserId], [MarkedByUserName], [MarkedUtc], [ChangeTitle])
                SELECT c.[Id], c.[ProjectId], @environment, @action,
                       @userId, @userName, @markedUtc, c.[Title]
                FROM [dbo].[ChangeRequests] c
                WHERE c.[Id] = @changeRequestId;
                """,
                new
                {
                    changeRequestId,
                    environment = (int)environment,
                    action = (int)(deployed ? DeploymentAction.Marked : DeploymentAction.Unmarked),
                    userId,
                    userName,
                    markedUtc
                },
                transaction,
                cancellationToken: cancellationToken));

            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<IReadOnlyList<ChangeRequest>> GetPendingChangesAsync(
        Guid projectId,
        DeploymentEnvironment environment,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        // Oldest first: the combined script has to replay in the order the changes were applied.
        var sql = environment == DeploymentEnvironment.Development
            ? """
              SELECT * FROM [dbo].[ChangeRequests]
              WHERE [ProjectId] = @projectId AND [Status] = @applied AND [DeployedToDevUtc] IS NULL
              ORDER BY [AppliedUtc], [SubmittedUtc];
              """
            : """
              SELECT * FROM [dbo].[ChangeRequests]
              WHERE [ProjectId] = @projectId AND [Status] = @applied AND [DeployedToProductionUtc] IS NULL
              ORDER BY [AppliedUtc], [SubmittedUtc];
              """;

        var rows = await connection.QueryAsync<ChangeRequest>(new CommandDefinition(
            sql,
            new { projectId, applied = (int)ChangeRequestStatus.Applied },
            cancellationToken: cancellationToken));

        return [.. rows];
    }

    public async Task<IReadOnlyList<ChangeDeployment>> GetDeploymentLogAsync(
        Guid projectId, int take = 100, CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        var rows = await connection.QueryAsync<ChangeDeployment>(new CommandDefinition(
            """
            SELECT TOP (@take) * FROM [dbo].[ChangeDeployments]
            WHERE [ProjectId] = @projectId
            ORDER BY [MarkedUtc] DESC, [Id] DESC;
            """,
            new { projectId, take },
            cancellationToken: cancellationToken));

        return [.. rows];
    }

    // ---- runs -------------------------------------------------------------------------

    public async Task AddRunAsync(GenerationRun run, CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO [dbo].[GenerationRuns]
                ([Id], [ProjectId], [ChangeRequestId], [RequestedByUserId], [RequestedByUserName],
                 [Status], [Stage], [OutputStyle], [QueuedUtc], [StartedUtc], [CompletedUtc],
                 [TableCount], [SkippedTableCount], [FileCount], [WarningCount], [ErrorCount],
                 [ErrorMessage], [ErrorDetail], [ArtifactId], [RequestedScope], [Scope], [ScopeNote])
            VALUES
                (@Id, @ProjectId, @ChangeRequestId, @RequestedByUserId, @RequestedByUserName,
                 @Status, @Stage, @OutputStyle, @QueuedUtc, @StartedUtc, @CompletedUtc,
                 @TableCount, @SkippedTableCount, @FileCount, @WarningCount, @ErrorCount,
                 @ErrorMessage, @ErrorDetail, @ArtifactId, @RequestedScope, @Scope, @ScopeNote);
            """,
            run,
            cancellationToken: cancellationToken));
    }

    public async Task UpdateRunAsync(GenerationRun run, CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE [dbo].[GenerationRuns] SET
                [Status] = @Status,
                [Stage] = @Stage,
                [StartedUtc] = @StartedUtc,
                [CompletedUtc] = @CompletedUtc,
                [TableCount] = @TableCount,
                [SkippedTableCount] = @SkippedTableCount,
                [FileCount] = @FileCount,
                [WarningCount] = @WarningCount,
                [ErrorCount] = @ErrorCount,
                [ErrorMessage] = @ErrorMessage,
                [ErrorDetail] = @ErrorDetail,
                [ArtifactId] = @ArtifactId,
                [Scope] = @Scope,
                [ScopeNote] = @ScopeNote
            WHERE [Id] = @Id;
            """,
            run,
            cancellationToken: cancellationToken));
    }

    public async Task<GenerationRun?> GetRunAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        return await connection.QuerySingleOrDefaultAsync<GenerationRun>(new CommandDefinition(
            """
            SELECT r.*, p.[Name] AS [ProjectName]
            FROM [dbo].[GenerationRuns] r
            LEFT JOIN [dbo].[Projects] p ON p.[Id] = r.[ProjectId]
            WHERE r.[Id] = @id;
            """,
            new { id },
            cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<GenerationRun>> GetRunsAsync(
        Guid projectId, int take = 50, CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        var rows = await connection.QueryAsync<GenerationRun>(new CommandDefinition(
            """
            SELECT TOP (@take) r.*, p.[Name] AS [ProjectName]
            FROM [dbo].[GenerationRuns] r
            LEFT JOIN [dbo].[Projects] p ON p.[Id] = r.[ProjectId]
            WHERE r.[ProjectId] = @projectId
            ORDER BY r.[QueuedUtc] DESC;
            """,
            new { projectId, take },
            cancellationToken: cancellationToken));

        return [.. rows];
    }

    public async Task<IReadOnlyList<GenerationRun>> GetUnfinishedRunsAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        var rows = await connection.QueryAsync<GenerationRun>(new CommandDefinition(
            """
            SELECT r.*, p.[Name] AS [ProjectName]
            FROM [dbo].[GenerationRuns] r
            LEFT JOIN [dbo].[Projects] p ON p.[Id] = r.[ProjectId]
            WHERE r.[Status] IN (@queued, @running)
            ORDER BY r.[QueuedUtc];
            """,
            new { queued = (int)RunStatus.Queued, running = (int)RunStatus.Running },
            cancellationToken: cancellationToken));

        return [.. rows];
    }

    // ---- schema snapshots --------------------------------------------------------------

    private static readonly JsonSerializerOptions SnapshotJson = new(JsonSerializerDefaults.Web);

    public async Task SaveSnapshotAsync(
        Guid runId, Guid projectId, SchemaSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        byte[] payload;

        using (var buffer = new MemoryStream())
        {
            await using (var gzip = new GZipStream(buffer, CompressionLevel.Optimal, leaveOpen: true))
            {
                await JsonSerializer.SerializeAsync(gzip, snapshot, SnapshotJson, cancellationToken);
            }

            payload = buffer.ToArray();
        }

        await using var connection = await connectionFactory.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            // Only the latest successful run is ever compared against, so older ones go.
            await connection.ExecuteAsync(new CommandDefinition(
                """
                DELETE FROM [dbo].[RunSchemaSnapshots] WHERE [ProjectId] = @projectId;
                INSERT INTO [dbo].[RunSchemaSnapshots] ([RunId], [ProjectId], [CapturedUtc], [Snapshot])
                VALUES (@runId, @projectId, @capturedUtc, @payload);
                """,
                new { runId, projectId, capturedUtc = snapshot.CapturedUtc, payload },
                transaction,
                cancellationToken: cancellationToken));

            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<SchemaSnapshot?> GetLatestSnapshotAsync(
        Guid projectId, Guid excludingRunId, CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        var payload = await connection.QuerySingleOrDefaultAsync<byte[]>(new CommandDefinition(
            """
            SELECT TOP 1 s.[Snapshot]
            FROM [dbo].[RunSchemaSnapshots] s
            INNER JOIN [dbo].[GenerationRuns] r ON r.[Id] = s.[RunId]
            WHERE s.[ProjectId] = @projectId AND s.[RunId] <> @excludingRunId AND r.[Status] = @succeeded
            ORDER BY s.[CapturedUtc] DESC;
            """,
            new { projectId, excludingRunId, succeeded = (int)RunStatus.Succeeded },
            cancellationToken: cancellationToken));

        if (payload is null)
        {
            return null;
        }

        try
        {
            using var buffer = new MemoryStream(payload);
            await using var gzip = new GZipStream(buffer, CompressionMode.Decompress);
            return await JsonSerializer.DeserializeAsync<SchemaSnapshot>(gzip, SnapshotJson, cancellationToken);
        }
        catch (Exception ex) when (ex is JsonException or InvalidDataException)
        {
            // An unreadable snapshot is treated as no baseline: the run then generates every table.
            return null;
        }
    }

    // ---- logs -------------------------------------------------------------------------

    public async Task AddLogAsync(RunLogEntry entry, CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO [dbo].[RunLogEntries] ([RunId], [LoggedUtc], [Level], [Stage], [Message], [Detail], [TableName])
            VALUES (@RunId, @LoggedUtc, @Level, @Stage, @Message, @Detail, @TableName);
            """,
            entry,
            cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<RunLogEntry>> GetLogsAsync(Guid runId, CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        var rows = await connection.QueryAsync<RunLogEntry>(new CommandDefinition(
            "SELECT * FROM [dbo].[RunLogEntries] WHERE [RunId] = @runId ORDER BY [Id];",
            new { runId },
            cancellationToken: cancellationToken));

        return [.. rows];
    }

    // ---- artifacts --------------------------------------------------------------------

    public async Task AddArtifactAsync(
        GeneratedArtifact artifact,
        IReadOnlyList<GeneratedFileEntry> files,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO [dbo].[GeneratedArtifacts]
                    ([Id], [RunId], [ProjectId], [FileName], [StoragePath], [SizeBytes], [Sha256],
                     [FileCount], [CreatedUtc], [IsAvailable])
                VALUES
                    (@Id, @RunId, @ProjectId, @FileName, @StoragePath, @SizeBytes, @Sha256,
                     @FileCount, @CreatedUtc, @IsAvailable);
                """,
                artifact,
                transaction,
                cancellationToken: cancellationToken));

            if (files.Count > 0)
            {
                // Dapper expands the list into one parameterised insert per row, in a single round trip.
                await connection.ExecuteAsync(new CommandDefinition(
                    """
                    INSERT INTO [dbo].[GeneratedFiles] ([ArtifactId], [RunId], [RelativePath], [Emitter], [TableName], [SizeBytes])
                    VALUES (@ArtifactId, @RunId, @RelativePath, @Emitter, @TableName, @SizeBytes);
                    """,
                    files,
                    transaction,
                    cancellationToken: cancellationToken));
            }

            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<GeneratedArtifact?> GetArtifactAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        return await connection.QuerySingleOrDefaultAsync<GeneratedArtifact>(new CommandDefinition(
            "SELECT * FROM [dbo].[GeneratedArtifacts] WHERE [Id] = @id;",
            new { id },
            cancellationToken: cancellationToken));
    }

    public async Task<GeneratedArtifact?> GetArtifactForRunAsync(Guid runId, CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        return await connection.QuerySingleOrDefaultAsync<GeneratedArtifact>(new CommandDefinition(
            "SELECT * FROM [dbo].[GeneratedArtifacts] WHERE [RunId] = @runId;",
            new { runId },
            cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<GeneratedFileEntry>> GetArtifactFilesAsync(
        Guid artifactId, CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        var rows = await connection.QueryAsync<GeneratedFileEntry>(new CommandDefinition(
            "SELECT * FROM [dbo].[GeneratedFiles] WHERE [ArtifactId] = @artifactId ORDER BY [RelativePath];",
            new { artifactId },
            cancellationToken: cancellationToken));

        return [.. rows];
    }

    public async Task MarkArtifactUnavailableAsync(Guid artifactId, CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE [dbo].[GeneratedArtifacts] SET [IsAvailable] = 0 WHERE [Id] = @artifactId;",
            new { artifactId },
            cancellationToken: cancellationToken));
    }
}
