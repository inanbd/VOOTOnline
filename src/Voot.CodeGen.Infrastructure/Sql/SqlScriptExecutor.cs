using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Voot.CodeGen.Application.Abstractions;
using Voot.CodeGen.Application.Models;

namespace Voot.CodeGen.Infrastructure.Sql;

/// <summary>
/// Runs a submitted script against a project's own database, one GO-delimited batch at a time.
/// </summary>
/// <remarks>
/// This executes arbitrary DDL by design: applying table changes is the feature. The blast
/// radius is bounded by the project's connection string, which only an administrator can set,
/// and every script is recorded against the user who submitted it before it runs.
/// </remarks>
public sealed class SqlScriptExecutor(ILogger<SqlScriptExecutor> logger) : ISqlScriptExecutor
{
    /// <summary>Applies to each batch, not to the script as a whole.</summary>
    private const int BatchTimeoutSeconds = 300;

    public async Task<SqlExecutionResult> ExecuteAsync(
        string connectionString,
        string sqlText,
        bool useTransaction,
        CancellationToken cancellationToken = default)
    {
        var batches = SqlBatchSplitter.Split(sqlText);

        if (batches.Count == 0)
        {
            return SqlExecutionResult.Failure("The script contained no executable statements.", 0);
        }

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        SqlTransaction? transaction = null;

        if (useTransaction)
        {
            transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);
        }

        var executed = 0;
        var rowsAffected = 0;

        try
        {
            for (var i = 0; i < batches.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                await using var command = connection.CreateCommand();
                command.CommandText = batches[i];
                command.CommandTimeout = BatchTimeoutSeconds;
                command.Transaction = transaction;

                var affected = await command.ExecuteNonQueryAsync(cancellationToken);

                if (affected > 0)
                {
                    rowsAffected += affected;
                }

                executed++;
            }

            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }

            return SqlExecutionResult.Ok(executed, rowsAffected);
        }
        catch (SqlException ex)
        {
            await RollbackAsync(transaction, logger);

            logger.LogWarning(
                ex,
                "Submitted SQL failed in batch {Batch} of {Total} (error {Number}).",
                executed + 1,
                batches.Count,
                ex.Number);

            return SqlExecutionResult.Failure(
                ex.Message,
                useTransaction ? 0 : executed,
                ex.Number,
                ex.LineNumber,
                executed);
        }
        catch (Exception ex)
        {
            await RollbackAsync(transaction, logger);

            logger.LogWarning(ex, "Submitted SQL failed before completing.");

            return SqlExecutionResult.Failure(ex.Message, useTransaction ? 0 : executed, batchIndex: executed);
        }
        finally
        {
            if (transaction is not null)
            {
                await transaction.DisposeAsync();
            }
        }
    }

    private static async Task RollbackAsync(SqlTransaction? transaction, ILogger logger)
    {
        if (transaction is null)
        {
            return;
        }

        try
        {
            await transaction.RollbackAsync();
        }
        catch (Exception ex)
        {
            // A rollback failure must not mask the original error the caller needs to see.
            logger.LogError(ex, "Rolling back the submitted SQL failed.");
        }
    }
}
