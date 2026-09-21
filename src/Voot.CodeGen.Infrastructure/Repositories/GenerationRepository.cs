using Dapper;
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
                 [ErrorMessage], [ErrorNumber], [ErrorLineNumber])
            VALUES
                (@Id, @ProjectId, @SubmittedByUserId, @SubmittedByUserName, @Title, @SqlText,
                 @Status, @SubmittedUtc, @AppliedUtc, @BatchesExecuted, @RowsAffected,
                 @ErrorMessage, @ErrorNumber, @ErrorLineNumber);
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
                [ErrorLineNumber] = @ErrorLineNumber
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
                 [ErrorMessage], [ErrorDetail], [ArtifactId])
            VALUES
                (@Id, @ProjectId, @ChangeRequestId, @RequestedByUserId, @RequestedByUserName,
                 @Status, @Stage, @OutputStyle, @QueuedUtc, @StartedUtc, @CompletedUtc,
                 @TableCount, @SkippedTableCount, @FileCount, @WarningCount, @ErrorCount,
                 @ErrorMessage, @ErrorDetail, @ArtifactId);
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
                [ArtifactId] = @ArtifactId
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
