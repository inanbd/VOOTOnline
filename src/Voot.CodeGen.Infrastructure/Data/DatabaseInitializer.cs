using System.Reflection;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Voot.CodeGen.Infrastructure.Sql;

namespace Voot.CodeGen.Infrastructure.Data;

/// <summary>
/// Applies the embedded schema scripts at startup and records each one, so the application
/// gets its tables without Entity Framework migrations. Scripts are idempotent and run in
/// name order; a script already recorded is skipped.
/// </summary>
public sealed class DatabaseInitializer(ISqlConnectionFactory connectionFactory, ILogger<DatabaseInitializer> logger)
{
    private const string ScriptNamespace = "Voot.CodeGen.Infrastructure.Data.Scripts.";

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await EnsureDatabaseExistsAsync(cancellationToken);

        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        await connection.ExecuteAsync(new CommandDefinition(
            """
            IF OBJECT_ID(N'[dbo].[__SchemaVersions]', N'U') IS NULL
                CREATE TABLE [dbo].[__SchemaVersions]
                (
                    [ScriptName] nvarchar(200)     NOT NULL CONSTRAINT [PK___SchemaVersions] PRIMARY KEY,
                    [AppliedUtc] datetimeoffset(7) NOT NULL CONSTRAINT [DF___SchemaVersions_Applied] DEFAULT (SYSUTCDATETIME())
                );
            """,
            cancellationToken: cancellationToken));

        var applied = (await connection.QueryAsync<string>(new CommandDefinition(
            "SELECT [ScriptName] FROM [dbo].[__SchemaVersions];", cancellationToken: cancellationToken)))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var (name, sql) in LoadScripts())
        {
            if (applied.Contains(name))
            {
                continue;
            }

            logger.LogInformation("Applying schema script {Script}.", name);

            // Scripts may use GO, which the server does not understand.
            foreach (var batch in SqlBatchSplitter.Split(sql))
            {
                await connection.ExecuteAsync(new CommandDefinition(
                    batch, commandTimeout: 180, cancellationToken: cancellationToken));
            }

            await connection.ExecuteAsync(new CommandDefinition(
                "INSERT INTO [dbo].[__SchemaVersions] ([ScriptName]) VALUES (@name);",
                new { name },
                cancellationToken: cancellationToken));
        }
    }

    /// <summary>
    /// Creates the application database if it is missing, by connecting to master on the
    /// same server. Lets a fresh deployment start against an empty SQL Server instance.
    /// </summary>
    private async Task EnsureDatabaseExistsAsync(CancellationToken cancellationToken)
    {
        var builder = new SqlConnectionStringBuilder(connectionFactory.ConnectionString);
        var databaseName = builder.InitialCatalog;

        if (string.IsNullOrWhiteSpace(databaseName))
        {
            throw new InvalidOperationException(
                "The application connection string must name a database (Initial Catalog).");
        }

        builder.InitialCatalog = "master";

        await using var connection = new SqlConnection(builder.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        var exists = await connection.ExecuteScalarAsync<int?>(new CommandDefinition(
            "SELECT 1 FROM sys.databases WHERE name = @databaseName;",
            new { databaseName },
            cancellationToken: cancellationToken));

        if (exists is not null)
        {
            return;
        }

        logger.LogInformation("Creating application database {Database}.", databaseName);

        // The name comes from configuration, not from a request, and CREATE DATABASE takes no
        // parameters; quote it anyway so an awkward name cannot break out of the statement.
        var quoted = "[" + databaseName.Replace("]", "]]", StringComparison.Ordinal) + "]";

        await connection.ExecuteAsync(new CommandDefinition(
            $"CREATE DATABASE {quoted};", cancellationToken: cancellationToken));
    }

    private static IEnumerable<(string Name, string Sql)> LoadScripts()
    {
        var assembly = Assembly.GetExecutingAssembly();

        var names = assembly.GetManifestResourceNames()
            .Where(n => n.StartsWith(ScriptNamespace, StringComparison.Ordinal) &&
                        n.EndsWith(".sql", StringComparison.OrdinalIgnoreCase))
            .OrderBy(n => n, StringComparer.Ordinal);

        foreach (var resourceName in names)
        {
            using var stream = assembly.GetManifestResourceStream(resourceName)
                ?? throw new InvalidOperationException($"Embedded script {resourceName} could not be opened.");

            using var reader = new StreamReader(stream);

            yield return (resourceName[ScriptNamespace.Length..], reader.ReadToEnd());
        }
    }
}
