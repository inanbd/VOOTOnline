namespace Voot.CodeGen.Domain.Generation;

/// <summary>
/// A message raised while generating a single table, e.g. "skipped, no primary key".
/// Collected per run so the UI can show partial failures instead of losing the whole batch.
/// </summary>
public sealed class GenerationDiagnostic
{
    public required DiagnosticSeverity Severity { get; init; }

    /// <summary>The table the diagnostic relates to; null for run-wide messages.</summary>
    public string? TableName { get; init; }

    /// <summary>The emitter that raised it, e.g. <c>MainDataAccess</c>; null for run-wide messages.</summary>
    public string? Emitter { get; init; }

    public required string Message { get; init; }

    /// <summary>Exception detail when the diagnostic came from a thrown exception.</summary>
    public string? Detail { get; init; }

    public static GenerationDiagnostic Info(string message, string? table = null) =>
        new() { Severity = DiagnosticSeverity.Information, Message = message, TableName = table };

    public static GenerationDiagnostic Warning(string message, string? table = null) =>
        new() { Severity = DiagnosticSeverity.Warning, Message = message, TableName = table };

    public static GenerationDiagnostic Error(string message, string? table = null, string? emitter = null, string? detail = null) =>
        new() { Severity = DiagnosticSeverity.Error, Message = message, TableName = table, Emitter = emitter, Detail = detail };

    public override string ToString() =>
        TableName is null ? $"[{Severity}] {Message}" : $"[{Severity}] {TableName}: {Message}";
}
