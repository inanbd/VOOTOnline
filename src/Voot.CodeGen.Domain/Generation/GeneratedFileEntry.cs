namespace Voot.CodeGen.Domain.Generation;

/// <summary>
/// One file inside a generated archive. Recorded per run so the project history can answer
/// "which files did this change produce?" without unpacking the zip.
/// </summary>
public sealed class GeneratedFileEntry
{
    public long Id { get; set; }

    public required Guid ArtifactId { get; set; }

    public required Guid RunId { get; set; }

    /// <summary>Path inside the archive, e.g. <c>HS/Entities/Bases/UserBase.cs</c>.</summary>
    public required string RelativePath { get; set; }

    /// <summary>Emitter that produced it, e.g. <c>BaseEntity</c>.</summary>
    public required string Emitter { get; set; }

    /// <summary>Source table, or null for run-level files.</summary>
    public string? TableName { get; set; }

    public int SizeBytes { get; set; }
}
