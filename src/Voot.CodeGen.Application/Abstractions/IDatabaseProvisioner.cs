namespace Voot.CodeGen.Application.Abstractions;

/// <summary>
/// Creates new, empty project databases on the one SQL Server an administrator configured for
/// the purpose. Users only ever supply a database name; the server and its credentials come
/// from configuration.
/// </summary>
public interface IDatabaseProvisioner
{
    /// <summary>False when no server is configured, in which case new databases cannot be created.</summary>
    bool IsConfigured { get; }

    /// <summary>The configured server's name, safe to show; null when none is configured.</summary>
    string? ServerName { get; }

    Task<bool> DatabaseExistsAsync(string databaseName, CancellationToken cancellationToken = default);

    /// <summary>Creates the database and returns a connection string pointing at it.</summary>
    Task<string> CreateDatabaseAsync(string databaseName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Drops a database this application has just created, when the project that was to use
    /// it could not be saved. Never called for a database the application did not create.
    /// </summary>
    Task DropDatabaseAsync(string databaseName, CancellationToken cancellationToken = default);
}
