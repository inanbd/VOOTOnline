namespace Voot.CodeGen.Generation;

/// <summary>A single file produced by an emitter, ready to be written into the archive.</summary>
public sealed class GeneratedFile
{
    /// <summary>Path inside the archive using forward slashes, e.g. <c>HS/Entities/Bases/UserBase.cs</c>.</summary>
    public required string RelativePath { get; init; }

    public required string Content { get; init; }

    /// <summary>Name of the emitter that produced the file, recorded in the run's file log.</summary>
    public required string Emitter { get; init; }

    /// <summary>Source table, or null for files that are not per-table.</summary>
    public string? TableName { get; init; }

    public string FileName => RelativePath[(RelativePath.LastIndexOf('/') + 1)..];
}
