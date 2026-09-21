using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Voot.CodeGen.Application.Abstractions;
using Voot.CodeGen.Infrastructure.Data;
using Dapper;

namespace Voot.CodeGen.Infrastructure.Storage;

/// <summary>
/// Deletes archive bytes older than the configured retention window. The artifact and file
/// rows stay: the history of what was generated is the audit record and is never pruned.
/// </summary>
public sealed class ArtifactRetentionService(
    ISqlConnectionFactory connectionFactory,
    IArtifactStorage storage,
    IOptions<ArtifactStorageOptions> options,
    ILogger<ArtifactRetentionService> logger)
{
    public async Task<int> PruneAsync(CancellationToken cancellationToken = default)
    {
        var days = options.Value.RetentionDays;

        if (days <= 0)
        {
            return 0;
        }

        var cutoff = DateTimeOffset.UtcNow.AddDays(-days);

        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        var expired = (await connection.QueryAsync<ExpiredArtifact>(new CommandDefinition(
            """
            SELECT [Id], [StoragePath] FROM [dbo].[GeneratedArtifacts]
            WHERE [IsAvailable] = 1 AND [CreatedUtc] < @cutoff;
            """,
            new { cutoff },
            cancellationToken: cancellationToken))).ToList();

        var removed = 0;

        foreach (var (id, storagePath) in expired.Select(e => (e.Id, e.StoragePath)))
        {
            try
            {
                await storage.DeleteAsync(storagePath, cancellationToken);

                await connection.ExecuteAsync(new CommandDefinition(
                    "UPDATE [dbo].[GeneratedArtifacts] SET [IsAvailable] = 0 WHERE [Id] = @id;",
                    new { id },
                    cancellationToken: cancellationToken));

                removed++;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Could not prune artifact {ArtifactId}.", id);
            }
        }

        if (removed > 0)
        {
            logger.LogInformation("Pruned {Count} archive(s) older than {Days} days.", removed, days);
        }

        return removed;
    }

    /// <summary>Dapper maps by column name, which rules out a ValueTuple here.</summary>
    private sealed record ExpiredArtifact(Guid Id, string StoragePath);
}
