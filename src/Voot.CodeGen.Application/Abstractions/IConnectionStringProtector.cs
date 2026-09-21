using Voot.CodeGen.Application.Models;

namespace Voot.CodeGen.Application.Abstractions;

/// <summary>
/// Encrypts project connection strings at rest. Implemented with ASP.NET Core Data
/// Protection, so the keys live outside the database the strings are stored in.
/// </summary>
public interface IConnectionStringProtector
{
    /// <summary>Validates, encrypts and redacts a connection string.</summary>
    /// <exception cref="Domain.Common.DomainException">The string is malformed.</exception>
    ConnectionStringInfo Protect(string connectionString);

    /// <summary>Decrypts a stored connection string for use against the target database.</summary>
    string Unprotect(string protectedConnectionString);
}
