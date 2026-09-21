using System.Text;

namespace Voot.CodeGen.Infrastructure.Sql;

/// <summary>
/// Splits a script on <c>GO</c> batch separators the way SQL Server's tooling does.
/// <c>GO</c> is a client directive, not T-SQL, so the server rejects a script containing it;
/// the executor has to break the script up itself.
/// </summary>
public static class SqlBatchSplitter
{
    /// <summary>
    /// Returns the non-empty batches of a script. A <c>GO</c> only separates when it stands
    /// alone on a line, so the word inside a string literal, a comment or an identifier is
    /// left untouched.
    /// </summary>
    public static IReadOnlyList<string> Split(string script)
    {
        if (string.IsNullOrWhiteSpace(script))
        {
            return [];
        }

        var batches = new List<string>();
        var current = new StringBuilder();

        var inLineComment = false;
        var inBlockComment = 0;
        var inString = false;
        var inBracket = false;
        var inQuotedIdentifier = false;

        var lines = script.Split('\n');

        foreach (var rawLine in lines)
        {
            var line = rawLine.TrimEnd('\r');

            // A GO only counts when the parser is not inside a literal or comment.
            if (!inString && !inBracket && !inQuotedIdentifier && inBlockComment == 0 && IsBatchSeparator(line))
            {
                AddBatch(batches, current);
                continue;
            }

            ScanLine(line, ref inLineComment, ref inBlockComment, ref inString, ref inBracket, ref inQuotedIdentifier);

            current.Append(line).Append('\n');
        }

        AddBatch(batches, current);
        return batches;
    }

    /// <summary>True for a line holding only <c>GO</c>, optionally followed by a repeat count.</summary>
    private static bool IsBatchSeparator(string line)
    {
        var trimmed = line.Trim();

        if (trimmed.Length < 2)
        {
            return false;
        }

        if (!trimmed.StartsWith("GO", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var rest = trimmed[2..].Trim();

        // Bare GO, "GO 5", or GO followed by a trailing comment.
        return rest.Length == 0
            || rest.StartsWith("--", StringComparison.Ordinal)
            || int.TryParse(rest, out _);
    }

    /// <summary>
    /// Tracks whether the end of the line leaves us inside a string, bracketed or quoted
    /// identifier, or block comment, so a GO on the next line is judged correctly.
    /// </summary>
    private static void ScanLine(
        string line,
        ref bool inLineComment,
        ref int inBlockComment,
        ref bool inString,
        ref bool inBracket,
        ref bool inQuotedIdentifier)
    {
        inLineComment = false;

        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            var next = i + 1 < line.Length ? line[i + 1] : '\0';

            if (inLineComment)
            {
                break;
            }

            if (inBlockComment > 0)
            {
                if (c == '*' && next == '/')
                {
                    inBlockComment--;
                    i++;
                }
                else if (c == '/' && next == '*')
                {
                    inBlockComment++;
                    i++;
                }

                continue;
            }

            if (inString)
            {
                // '' is an escaped quote, not the end of the literal.
                if (c == '\'')
                {
                    if (next == '\'')
                    {
                        i++;
                    }
                    else
                    {
                        inString = false;
                    }
                }

                continue;
            }

            if (inBracket)
            {
                if (c == ']')
                {
                    if (next == ']')
                    {
                        i++;
                    }
                    else
                    {
                        inBracket = false;
                    }
                }

                continue;
            }

            if (inQuotedIdentifier)
            {
                if (c == '"')
                {
                    inQuotedIdentifier = false;
                }

                continue;
            }

            switch (c)
            {
                case '-' when next == '-':
                    inLineComment = true;
                    i++;
                    break;
                case '/' when next == '*':
                    inBlockComment++;
                    i++;
                    break;
                case '\'':
                    inString = true;
                    break;
                case '[':
                    inBracket = true;
                    break;
                case '"':
                    inQuotedIdentifier = true;
                    break;
                default:
                    break;
            }
        }
    }

    private static void AddBatch(List<string> batches, StringBuilder current)
    {
        var text = current.ToString();
        current.Clear();

        if (!string.IsNullOrWhiteSpace(text))
        {
            batches.Add(text.Trim());
        }
    }
}
