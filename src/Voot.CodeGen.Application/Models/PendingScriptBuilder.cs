using System.Globalization;
using System.Text;
using Voot.CodeGen.Domain.Generation;

namespace Voot.CodeGen.Application.Models;

/// <summary>
/// Stitches the changes still outstanding for an environment into one replayable script,
/// oldest first, with a header per change saying where it came from.
/// </summary>
public static class PendingScriptBuilder
{
    public static string Build(
        string projectName,
        DeploymentEnvironment environment,
        IReadOnlyList<ChangeRequest> pending,
        DateTimeOffset generatedUtc)
    {
        ArgumentNullException.ThrowIfNull(pending);

        var label = environment == DeploymentEnvironment.Development ? "development" : "production";
        var builder = new StringBuilder();

        builder.Append("-- ").Append(new string('=', 68)).Append('\n');
        builder.Append("-- Pending for ").Append(label).Append(": ")
            .Append(pending.Count).Append(pending.Count == 1 ? " change" : " changes").Append('\n');
        builder.Append("-- Project: ").Append(projectName).Append('\n');
        builder.Append("-- Generated: ")
            .Append(generatedUtc.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture))
            .Append(" UTC\n");
        builder.Append("-- Ordered oldest first; run in this order.\n");
        builder.Append("-- ").Append(new string('=', 68)).Append("\n\n");

        if (pending.Count == 0)
        {
            builder.Append("-- Nothing outstanding: every applied change is already marked as reaching ")
                .Append(label).Append(".\n");
            return builder.ToString();
        }

        foreach (var change in pending)
        {
            var when = (change.AppliedUtc ?? change.SubmittedUtc)
                .ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

            builder.Append("-- ---------------------------------------------------------------\n");
            builder.Append("-- ").Append(when).Append(" UTC");

            if (!string.IsNullOrWhiteSpace(change.SubmittedByUserName))
            {
                builder.Append(" by ").Append(change.SubmittedByUserName);
            }

            builder.Append('\n');

            if (!string.IsNullOrWhiteSpace(change.Title))
            {
                builder.Append("-- ").Append(change.Title).Append('\n');
            }

            if (!string.IsNullOrWhiteSpace(change.StructureSummary))
            {
                builder.Append("-- ").Append(change.StructureSummary).Append('\n');
            }

            builder.Append("-- ---------------------------------------------------------------\n");
            builder.Append(change.SqlText.TrimEnd()).Append('\n');

            // A GO between changes keeps each script in its own batch, so a CREATE PROCEDURE or
            // a CREATE SCHEMA in one does not swallow the next.
            if (!EndsWithBatchSeparator(change.SqlText))
            {
                builder.Append("GO\n");
            }

            builder.Append('\n');
        }

        return builder.ToString();
    }

    private static bool EndsWithBatchSeparator(string sql)
    {
        var lastLine = sql.TrimEnd().Split('\n').LastOrDefault()?.Trim();

        return lastLine is not null
            && lastLine.StartsWith("GO", StringComparison.OrdinalIgnoreCase)
            && lastLine.Length <= 4;
    }
}
