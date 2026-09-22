using Voot.CodeGen.Application.Models;
using Voot.CodeGen.Domain.Schema;

namespace Voot.CodeGen.Application.Abstractions;

/// <summary>Reads a database's structure into the generator's schema model.</summary>
public interface ISchemaReader
{
    Task<DatabaseModel> ReadAsync(string connectionString, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the tables and their column counts without reading columns, keys or indexes.
    /// Used by the schema browser's picker, which only needs names.
    /// </summary>
    Task<IReadOnlyList<TableSummary>> ListTablesAsync(
        string connectionString, CancellationToken cancellationToken = default);

    /// <summary>Opens a connection and returns null on success, or the failure message.</summary>
    Task<string?> TestConnectionAsync(string connectionString, CancellationToken cancellationToken = default);
}
