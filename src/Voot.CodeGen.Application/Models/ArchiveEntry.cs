namespace Voot.CodeGen.Application.Models;

/// <summary>One file destined for the generated archive.</summary>
/// <param name="Path">Path inside the archive, using forward slashes.</param>
/// <param name="Content">UTF-8 text content.</param>
public readonly record struct ArchiveEntry(string Path, string Content);
