using System.Text;
using System.Text.RegularExpressions;

namespace Voot.CodeGen.Application.Models;

/// <summary>A statement in a script that reaches outside the database the script runs against.</summary>
/// <param name="Line">1-based line the statement starts on.</param>
/// <param name="Text">That line, trimmed, for the message.</param>
public sealed record ScriptIssue(int Line, string Text);

/// <summary>
/// Finds statements that switch to, create, alter or drop a database. A script uploaded for a
/// project has to run against that project's database, but scripts exported from SQL Server
/// Management Studio usually start with <c>USE [SourceDatabase]</c>, which would silently apply
/// the rest of the script to a different database on the same server.
/// </summary>
public static partial class SqlScriptInspector
{
    public static IReadOnlyList<ScriptIssue> FindDatabaseLevelStatements(string script)
    {
        if (string.IsNullOrEmpty(script))
        {
            return [];
        }

        var code = MaskNonCode(script);
        var lines = script.Split('\n');
        var issues = new List<ScriptIssue>();

        foreach (Match match in DatabaseStatement().Matches(code))
        {
            var line = 1 + code.AsSpan(0, match.Index).Count('\n');
            var text = lines[line - 1].Trim();

            issues.Add(new ScriptIssue(line, text.Length > 120 ? text[..120] + "…" : text));
        }

        return issues;
    }

    /// <summary>
    /// Blanks out comments, string literals and delimited identifiers, keeping line breaks, so
    /// only real keywords are left to match: <c>'USE x'</c>, <c>-- USE x</c> and <c>[USE]</c> are ignored.
    /// </summary>
    internal static string MaskNonCode(string script)
    {
        var output = new StringBuilder(script.Length);
        var i = 0;

        while (i < script.Length)
        {
            var c = script[i];
            var next = i + 1 < script.Length ? script[i + 1] : '\0';

            if (c == '-' && next == '-')
            {
                while (i < script.Length && script[i] != '\n')
                {
                    output.Append(' ');
                    i++;
                }
            }
            else if (c == '/' && next == '*')
            {
                // Block comments nest in T-SQL.
                var depth = 0;

                while (i < script.Length)
                {
                    if (script[i] == '/' && i + 1 < script.Length && script[i + 1] == '*')
                    {
                        depth++;
                        output.Append("  ");
                        i += 2;
                    }
                    else if (script[i] == '*' && i + 1 < script.Length && script[i + 1] == '/')
                    {
                        depth--;
                        output.Append("  ");
                        i += 2;

                        if (depth == 0)
                        {
                            break;
                        }
                    }
                    else
                    {
                        output.Append(script[i] == '\n' ? '\n' : ' ');
                        i++;
                    }
                }
            }
            else if (c is '\'' or '[' or '"')
            {
                var close = c == '[' ? ']' : c;
                output.Append(' ');
                i++;

                while (i < script.Length)
                {
                    if (script[i] == close)
                    {
                        // A doubled closing character is an escape, not the end.
                        if (i + 1 < script.Length && script[i + 1] == close)
                        {
                            output.Append("  ");
                            i += 2;
                            continue;
                        }

                        output.Append(' ');
                        i++;
                        break;
                    }

                    output.Append(script[i] == '\n' ? '\n' : ' ');
                    i++;
                }
            }
            else
            {
                output.Append(c);
                i++;
            }
        }

        return output.ToString();
    }

    // USE, or CREATE/ALTER/DROP DATABASE — but not the database-scoped statements that share
    // the keyword (DATABASE SCOPED CONFIGURATION/CREDENTIAL, DATABASE AUDIT SPECIFICATION,
    // DATABASE ENCRYPTION KEY) or ALTER DATABASE CURRENT, which all stay in the current database.
    [GeneratedRegex(
        @"(?<![\w@#$])(?:USE|(?:CREATE|ALTER|DROP)\s+DATABASE(?!\s+(?:SCOPED|AUDIT|ENCRYPTION|CURRENT)(?![\w@#$])))(?![\w@#$])",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex DatabaseStatement();
}
