namespace Voot.CodeGen.Application.Models;

/// <summary>
/// A validated connection string, split into the protected form that is persisted and the
/// redacted form that is safe to show in the UI.
/// </summary>
/// <param name="Protected">Encrypted text, written to the project row.</param>
/// <param name="Summary">Server and database only, with credentials removed.</param>
public readonly record struct ConnectionStringInfo(string Protected, string Summary);
