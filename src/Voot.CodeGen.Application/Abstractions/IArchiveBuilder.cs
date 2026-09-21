using Voot.CodeGen.Application.Models;

namespace Voot.CodeGen.Application.Abstractions;

/// <summary>Packs generated files into a zip.</summary>
public interface IArchiveBuilder
{
    /// <summary>Writes the entries into <paramref name="destination"/> as a zip archive.</summary>
    Task BuildAsync(
        IEnumerable<ArchiveEntry> entries,
        Stream destination,
        CancellationToken cancellationToken = default);
}
