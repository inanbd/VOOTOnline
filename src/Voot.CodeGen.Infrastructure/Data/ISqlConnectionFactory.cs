using System.Data.Common;

namespace Voot.CodeGen.Infrastructure.Data;

/// <summary>
/// Opens connections to the application's own database. Target databases belonging to
/// projects are never reached through this: those use the project's own connection string.
/// </summary>
public interface ISqlConnectionFactory
{
    Task<DbConnection> OpenAsync(CancellationToken cancellationToken = default);

    string ConnectionString { get; }
}
