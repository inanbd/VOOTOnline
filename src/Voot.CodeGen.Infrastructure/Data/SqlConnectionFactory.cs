using System.Data.Common;
using Microsoft.Data.SqlClient;

namespace Voot.CodeGen.Infrastructure.Data;

/// <inheritdoc />
public sealed class SqlConnectionFactory(string connectionString) : ISqlConnectionFactory
{
    public string ConnectionString { get; } = connectionString;

    public async Task<DbConnection> OpenAsync(CancellationToken cancellationToken = default)
    {
        var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }
}
