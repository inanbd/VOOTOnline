using Voot.CodeGen.Domain.Generation;

namespace Voot.CodeGen.Generation;

/// <summary>
/// Everything one generation pass produced. Files and diagnostics are both returned so a
/// partial failure still yields an archive plus a visible explanation of what went wrong.
/// </summary>
public sealed class GenerationResult
{
    public required IReadOnlyList<GeneratedFile> Files { get; init; }

    public required IReadOnlyList<GenerationDiagnostic> Diagnostics { get; init; }

    /// <summary>Tables that were generated.</summary>
    public int TableCount { get; init; }

    /// <summary>Tables skipped because they had no primary key.</summary>
    public int SkippedTableCount { get; init; }

    public int WarningCount => Diagnostics.Count(d => d.Severity == DiagnosticSeverity.Warning);

    public int ErrorCount => Diagnostics.Count(d => d.Severity == DiagnosticSeverity.Error);

    public bool HasErrors => ErrorCount > 0;
}
