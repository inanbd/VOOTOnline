using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Voot.CodeGen.Application.Abstractions;
using Voot.CodeGen.Application.Services;
using Voot.CodeGen.Domain.Common;

namespace Voot.CodeGen.Infrastructure.Sql;

public sealed class DatabaseProvisioningOptions
{
    /// <summary>
    /// A connection string for the server new project databases are created on. Its login
    /// needs CREATE DATABASE permission (for example the dbcreator role). Any database named in
    /// it is ignored; each new project gets this string with its own database name instead.
    /// Leave empty to turn off creating databases.
    /// </summary>
    public string? ServerConnectionString { get; set; }
}

/// <summary>Creates project databases on the configured SQL Server.</summary>
public sealed class SqlDatabaseProvisioner(
    IOptions<DatabaseProvisioningOptions> options,
    ILogger<SqlDatabaseProvisioner> logger) : IDatabaseProvisioner
{
    private const int CreateTimeoutSeconds = 300;

    // SQL Server error numbers.
    private const int DatabaseAlreadyExists = 1801;
    private const int PermissionDenied = 262;

    private readonly string? _serverConnectionString =
        string.IsNullOrWhiteSpace(options.Value.ServerConnectionString) ? null : options.Value.ServerConnectionString;

    public bool IsConfigured => _serverConnectionString is not null;

    public string? ServerName => _serverConnectionString is null
        ? null
        : new SqlConnectionStringBuilder(_serverConnectionString).DataSource;

    public async Task<bool> DatabaseExistsAsync(string databaseName, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenMasterAsync(cancellationToken);

        return await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT CASE WHEN DB_ID(@databaseName) IS NULL THEN 0 ELSE 1 END;",
            new { databaseName },
            cancellationToken: cancellationToken)) == 1;
    }

    public async Task<string> CreateDatabaseAsync(string databaseName, CancellationToken cancellationToken = default)
    {
        EnsureValid(databaseName);

        await using var connection = await OpenMasterAsync(cancellationToken);

        try
        {
            // CREATE DATABASE takes no parameters, so the name is quoted on the server.
            await connection.ExecuteAsync(new CommandDefinition(
                """
                DECLARE @sql nvarchar(max) = N'CREATE DATABASE ' + QUOTENAME(@databaseName) + N';';
                EXEC (@sql);
                """,
                new { databaseName },
                commandTimeout: CreateTimeoutSeconds,
                cancellationToken: cancellationToken));
        }
        catch (SqlException ex) when (ex.Number == DatabaseAlreadyExists)
        {
            throw new DomainException($"A database named '{databaseName}' already exists on {ServerName}.", ex);
        }
        catch (SqlException ex) when (ex.Number == PermissionDenied)
        {
            throw new DomainException(
                $"The login configured for new databases is not allowed to create them on {ServerName}. {ex.Message}", ex);
        }
        catch (SqlException ex)
        {
            throw new DomainException($"The database could not be created: {ex.Message}", ex);
        }

        logger.LogInformation("Created database {Database} on {Server}.", databaseName, ServerName);

        return new SqlConnectionStringBuilder(_serverConnectionString) { InitialCatalog = databaseName }.ConnectionString;
    }

    public async Task DropDatabaseAsync(string databaseName, CancellationToken cancellationToken = default)
    {
        EnsureValid(databaseName);

        await using var connection = await OpenMasterAsync(cancellationToken);

        await connection.ExecuteAsync(new CommandDefinition(
            """
            IF DB_ID(@databaseName) IS NOT NULL
            BEGIN
                DECLARE @sql nvarchar(max) = N'DROP DATABASE ' + QUOTENAME(@databaseName) + N';';
                EXEC (@sql);
            END
            """,
            new { databaseName },
            commandTimeout: CreateTimeoutSeconds,
            cancellationToken: cancellationToken));

        logger.LogWarning("Dropped database {Database} on {Server} after project setup failed.", databaseName, ServerName);
    }

    private async Task<SqlConnection> OpenMasterAsync(CancellationToken cancellationToken)
    {
        if (_serverConnectionString is null)
        {
            throw new DomainException(
                "Creating databases is not set up. An administrator has to set DatabaseProvisioning:ServerConnectionString.");
        }

        var builder = new SqlConnectionStringBuilder(_serverConnectionString) { InitialCatalog = "master" };
        var connection = new SqlConnection(builder.ConnectionString);

        try
        {
            await connection.OpenAsync(cancellationToken);
        }
        catch (SqlException ex)
        {
            await connection.DisposeAsync();
            throw new DomainException($"Could not connect to {ServerName} to create the database: {ex.Message}", ex);
        }

        return connection;
    }

    /// <summary>Checked here as well as by the caller, since this runs DDL built from the name.</summary>
    private static void EnsureValid(string databaseName)
    {
        if (DatabaseNameRule.Validate(databaseName) is { } error)
        {
            throw new DomainException(error);
        }
    }
}
