using System.IO.Compression;
using System.Text;
using Voot.CodeGen.Application.Abstractions;
using Voot.CodeGen.Application.Models;

namespace Voot.CodeGen.Infrastructure.Packaging;

/// <inheritdoc />
public sealed class ZipArchiveBuilder : IArchiveBuilder
{
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    public async Task BuildAsync(
        IEnumerable<ArchiveEntry> entries,
        Stream destination,
        CancellationToken cancellationToken = default)
    {
        // leaveOpen so the caller still owns the stream it handed us.
        using var archive = new ZipArchive(destination, ZipArchiveMode.Create, leaveOpen: true);

        foreach (var entry in entries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var path = NormalizeEntryPath(entry.Path);
            var zipEntry = archive.CreateEntry(path, CompressionLevel.Optimal);

            await using var stream = zipEntry.Open();
            await using var writer = new StreamWriter(stream, Utf8NoBom);
            await writer.WriteAsync(entry.Content.AsMemory(), cancellationToken);
        }
    }

    /// <summary>
    /// Rejects anything that would escape the archive root when extracted. Entry paths are
    /// generated rather than user-supplied, but a table name reaches them, so this is checked
    /// rather than assumed.
    /// </summary>
    internal static string NormalizeEntryPath(string path)
    {
        var normalized = path.Replace('\\', '/').TrimStart('/');

        if (normalized.Length == 0)
        {
            throw new InvalidOperationException("An archive entry had an empty path.");
        }

        var segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);

        if (segments.Any(s => s == ".." || s == "."))
        {
            throw new InvalidOperationException($"Archive entry path '{path}' is not allowed.");
        }

        if (Path.IsPathRooted(normalized) || normalized.Contains(':', StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Archive entry path '{path}' is not allowed.");
        }

        return string.Join('/', segments);
    }
}
