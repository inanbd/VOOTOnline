namespace Voot.CodeGen.Application.Abstractions;

/// <summary>
/// Stores generated archives outside the database so they stay re-downloadable from a
/// project's history.
/// </summary>
public interface IArtifactStorage
{
    /// <summary>
    /// Persists an archive and returns its storage-relative path plus size and SHA-256.
    /// </summary>
    Task<(string StoragePath, long SizeBytes, string Sha256)> SaveAsync(
        Guid projectId,
        Guid runId,
        string fileName,
        Func<Stream, CancellationToken, Task> writeContent,
        CancellationToken cancellationToken = default);

    /// <summary>Opens a stored archive for download, or null when the bytes are gone.</summary>
    Task<Stream?> OpenAsync(string storagePath, CancellationToken cancellationToken = default);

    Task DeleteAsync(string storagePath, CancellationToken cancellationToken = default);
}
