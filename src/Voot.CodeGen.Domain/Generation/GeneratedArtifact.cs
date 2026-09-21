namespace Voot.CodeGen.Domain.Generation;

/// <summary>
/// The zip produced by a run. Bytes live in artifact storage; this row is the durable
/// index entry so the archive stays re-downloadable from the project's history.
/// </summary>
public sealed class GeneratedArtifact
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public required Guid RunId { get; set; }

    public required Guid ProjectId { get; set; }

    /// <summary>Download file name, e.g. <c>MyProject-20260921-141530.zip</c>.</summary>
    public required string FileName { get; set; }

    /// <summary>Storage-relative path. Never rendered to users and never used to build a URL.</summary>
    public required string StoragePath { get; set; }

    public long SizeBytes { get; set; }

    /// <summary>Lowercase hex SHA-256 of the archive, so a re-download can be verified.</summary>
    public string? Sha256 { get; set; }

    public int FileCount { get; set; }

    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>False once a retention job has removed the bytes; the metadata row survives.</summary>
    public bool IsAvailable { get; set; } = true;
}
