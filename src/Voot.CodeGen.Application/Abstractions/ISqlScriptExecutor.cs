using Voot.CodeGen.Application.Models;

namespace Voot.CodeGen.Application.Abstractions;

/// <summary>
/// Runs a user-submitted script against a project's own database. Never used against the
/// application's database.
/// </summary>
public interface ISqlScriptExecutor
{
    /// <param name="useTransaction">
    /// Wrap all batches in one transaction and roll back on the first failure. Some DDL cannot
    /// run inside a transaction, so projects may turn this off.
    /// </param>
    Task<SqlExecutionResult> ExecuteAsync(
        string connectionString,
        string sqlText,
        bool useTransaction,
        CancellationToken cancellationToken = default);
}
