using Voot.CodeGen.Application.Abstractions;
using Voot.CodeGen.Domain.Common;

namespace Voot.CodeGen.Application.Services;

/// <summary>Serves a stored archive back to a user who may see the project it belongs to.</summary>
public sealed class ArtifactDownloadService(
    IGenerationRepository repository,
    IArtifactStorage storage,
    ProjectAccessService access)
{
    /// <summary>
    /// Opens an archive for download.
    /// </summary>
    /// <exception cref="NotFoundException">No such artifact, or its bytes have been removed.</exception>
    /// <exception cref="ForbiddenException">The caller is not assigned to the project.</exception>
    public async Task<(Stream Content, string FileName)> OpenAsync(
        Guid artifactId, CancellationToken cancellationToken = default)
    {
        var artifact = await repository.GetArtifactAsync(artifactId, cancellationToken)
            ?? throw new NotFoundException("Download");

        // Authorise against the project before touching storage.
        await access.RequireAccessAsync(artifact.ProjectId, cancellationToken);

        if (!artifact.IsAvailable)
        {
            throw new NotFoundException("This archive has been removed by the retention policy; regenerate to get a fresh one");
        }

        var stream = await storage.OpenAsync(artifact.StoragePath, cancellationToken);

        if (stream is null)
        {
            // The row says available but the bytes are gone; correct the record.
            await repository.MarkArtifactUnavailableAsync(artifact.Id, cancellationToken);
            throw new NotFoundException("The archive file is missing from storage; regenerate to get a fresh one");
        }

        return (stream, artifact.FileName);
    }
}
