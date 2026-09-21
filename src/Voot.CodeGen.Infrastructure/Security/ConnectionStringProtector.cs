using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.SqlClient;
using Voot.CodeGen.Application.Abstractions;
using Voot.CodeGen.Application.Models;
using Voot.CodeGen.Domain.Common;

namespace Voot.CodeGen.Infrastructure.Security;

/// <summary>
/// Encrypts project connection strings with ASP.NET Core Data Protection, so a database
/// backup on its own does not disclose the credentials of every target server.
/// </summary>
public sealed class ConnectionStringProtector : IConnectionStringProtector
{
    private const string Purpose = "Voot.CodeGen.ProjectConnectionString.v1";

    private readonly IDataProtector _protector;

    public ConnectionStringProtector(IDataProtectionProvider provider) =>
        _protector = provider.CreateProtector(Purpose);

    public ConnectionStringInfo Protect(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new DomainException("The connection string is required.");
        }

        SqlConnectionStringBuilder builder;

        try
        {
            builder = new SqlConnectionStringBuilder(connectionString);
        }
        catch (ArgumentException ex)
        {
            throw new DomainException($"The connection string could not be parsed: {ex.Message}", ex);
        }

        if (string.IsNullOrWhiteSpace(builder.DataSource))
        {
            throw new DomainException("The connection string must name a server (Data Source).");
        }

        if (string.IsNullOrWhiteSpace(builder.InitialCatalog))
        {
            throw new DomainException("The connection string must name a database (Initial Catalog).");
        }

        return new ConnectionStringInfo(_protector.Protect(connectionString), Summarize(builder));
    }

    public string Unprotect(string protectedConnectionString)
    {
        try
        {
            return _protector.Unprotect(protectedConnectionString);
        }
        catch (Exception ex)
        {
            // Usually a rotated or lost data-protection key ring.
            throw new DomainException(
                "The stored connection string could not be decrypted. Re-enter it on the project.", ex);
        }
    }

    /// <summary>Server and database only; credentials are dropped so this is safe to render.</summary>
    private static string Summarize(SqlConnectionStringBuilder builder)
    {
        var auth = builder.IntegratedSecurity
            ? "integrated"
            : string.IsNullOrEmpty(builder.UserID) ? "unspecified" : builder.UserID;

        return $"{builder.DataSource} / {builder.InitialCatalog} (user: {auth})";
    }
}
