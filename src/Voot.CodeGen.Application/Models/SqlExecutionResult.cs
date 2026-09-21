namespace Voot.CodeGen.Application.Models;

/// <summary>Outcome of running a user-submitted SQL script against a project's database.</summary>
public sealed class SqlExecutionResult
{
    public bool Success { get; init; }

    /// <summary>Number of GO-delimited batches that ran successfully.</summary>
    public int BatchesExecuted { get; init; }

    public int RowsAffected { get; init; }

    public string? ErrorMessage { get; init; }

    /// <summary>SQL Server error number when the failure came from the server.</summary>
    public int? ErrorNumber { get; init; }

    /// <summary>Line number within the failing batch.</summary>
    public int? ErrorLineNumber { get; init; }

    /// <summary>Zero-based index of the batch that failed.</summary>
    public int? FailedBatchIndex { get; init; }

    public static SqlExecutionResult Ok(int batches, int rows) =>
        new() { Success = true, BatchesExecuted = batches, RowsAffected = rows };

    public static SqlExecutionResult Failure(
        string message, int batchesExecuted, int? errorNumber = null, int? line = null, int? batchIndex = null) =>
        new()
        {
            Success = false,
            ErrorMessage = message,
            BatchesExecuted = batchesExecuted,
            ErrorNumber = errorNumber,
            ErrorLineNumber = line,
            FailedBatchIndex = batchIndex
        };
}
