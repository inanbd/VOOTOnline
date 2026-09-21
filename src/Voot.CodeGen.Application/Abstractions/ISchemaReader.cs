using Voot.CodeGen.Domain.Schema;

namespace Voot.CodeGen.Application.Abstractions;

/// <summary>Reads a database's structure into the generator's schema model.</summary>
public interface ISchemaReader
{
    Task<DatabaseModel> ReadAsync(string connectionString, CancellationToken cancellationToken = default);

    /// <summary>Opens a connection and returns null on success, or the failure message.</summary>
    Task<string?> TestConnectionAsync(string connectionString, CancellationToken cancellationToken = default);
}
