using System.Text.RegularExpressions;

namespace Voot.CodeGen.Application.Services;

/// <summary>
/// What a new database may be called. Deliberately narrower than SQL Server allows: letters,
/// digits and underscores, starting with a letter, so the name is safe everywhere it ends up —
/// connection strings, file names and scripts — without quoting.
/// </summary>
public static partial class DatabaseNameRule
{
    public const int MaxLength = 100;

    private static readonly HashSet<string> Reserved = new(StringComparer.OrdinalIgnoreCase)
    {
        "master", "model", "msdb", "tempdb", "distribution", "resource"
    };

    /// <summary>Returns why the name is not allowed, or null when it is.</summary>
    public static string? Validate(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "Enter a name for the new database.";
        }

        if (name.Length > MaxLength)
        {
            return $"The database name can be at most {MaxLength} characters.";
        }

        if (!Pattern().IsMatch(name))
        {
            return "The database name must start with a letter and contain only letters, digits and underscores.";
        }

        if (Reserved.Contains(name))
        {
            return $"'{name}' is a SQL Server system database name.";
        }

        return null;
    }

    [GeneratedRegex("^[A-Za-z][A-Za-z0-9_]*$")]
    private static partial Regex Pattern();
}
