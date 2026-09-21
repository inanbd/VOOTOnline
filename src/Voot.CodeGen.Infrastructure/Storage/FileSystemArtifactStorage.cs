using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using Voot.CodeGen.Application.Abstractions;

namespace Voot.CodeGen.Infrastructure.Storage;

public sealed class ArtifactStorageOptions
{
    /// <summary>Root directory for generated archives. Relative paths resolve against the content root.</summary>
    public string RootPath { get; set; } = "App_Data/artifacts";

    /// <summary>Days to keep archive bytes. Zero or less keeps them forever.</summary>
    public int RetentionDays { get; set; }
}

/// <summary>
/// Stores archives on disk under a project/run directory tree. Paths are built from ids
/// rather than user input, and the resolved path is checked to stay under the root.
/// </summary>
public sealed class FileSystemArtifactStorage : IArtifactStorage
{
    private readonly string _root;

    public FileSystemArtifactStorage(IOptions<ArtifactStorageOptions> options, string contentRootPath)
    {
        var configured = options.Value.RootPath;

        _root = Path.GetFullPath(
            Path.IsPathRooted(configured) ? configured : Path.Combine(contentRootPath, configured));

        Directory.CreateDirectory(_root);
    }

    public async Task<(string StoragePath, long SizeBytes, string Sha256)> SaveAsync(
        Guid projectId,
        Guid runId,
        string fileName,
        Func<Stream, CancellationToken, Task> writeContent,
        CancellationToken cancellationToken = default)
    {
        // Ids, not names, so nothing user-controlled reaches the path.
        var relativePath = Path.Combine(projectId.ToString("N"), runId.ToString("N"), "archive.zip");
        var fullPath = ResolveWithinRoot(relativePath);

        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        await using (var file = new FileStream(
            fullPath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 81920, useAsync: true))
        {
            await writeContent(file, cancellationToken);
        }

        var info = new FileInfo(fullPath);

        await using var forHash = new FileStream(
            fullPath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 81920, useAsync: true);

        var hash = await SHA256.HashDataAsync(forHash, cancellationToken);

        return (relativePath.Replace('\\', '/'), info.Length, Convert.ToHexString(hash).ToLowerInvariant());
    }

    public Task<Stream?> OpenAsync(string storagePath, CancellationToken cancellationToken = default)
    {
        var fullPath = ResolveWithinRoot(storagePath);

        if (!File.Exists(fullPath))
        {
            return Task.FromResult<Stream?>(null);
        }

        Stream stream = new FileStream(
            fullPath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 81920, useAsync: true);

        return Task.FromResult<Stream?>(stream);
    }

    public Task DeleteAsync(string storagePath, CancellationToken cancellationToken = default)
    {
        var fullPath = ResolveWithinRoot(storagePath);

        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);

            // Leave no empty run directory behind.
            var directory = Path.GetDirectoryName(fullPath);

            if (directory is not null &&
                Directory.Exists(directory) &&
                !Directory.EnumerateFileSystemEntries(directory).Any())
            {
                Directory.Delete(directory);
            }
        }

        return Task.CompletedTask;
    }

    /// <summary>Resolves a stored path and refuses anything that escapes the storage root.</summary>
    private string ResolveWithinRoot(string relativePath)
    {
        var combined = Path.GetFullPath(Path.Combine(_root, relativePath));

        if (!combined.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.Ordinal) &&
            !string.Equals(combined, _root, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The artifact path resolved outside the storage root.");
        }

        return combined;
    }
}
