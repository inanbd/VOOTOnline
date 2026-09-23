using System.Globalization;
using Voot.CodeGen.Application.Models;
using Voot.CodeGen.Domain.Generation;
using Voot.CodeGen.Domain.Schema;
using Voot.CodeGen.Generation;

namespace Voot.CodeGen.Application.Services;

/// <summary>What a run will generate, and why.</summary>
/// <param name="Scope">The scope the run will actually use.</param>
/// <param name="Selection">The tables to generate, or null for every table.</param>
/// <param name="Note">Why a changed-tables request fell back, or what the partial run covers.</param>
public sealed record ScopePlan(GenerationScope Scope, TableSelection? Selection, string? Note);

/// <summary>
/// Decides which tables a run generates. Pure: it compares the current schema with the snapshot
/// saved by the last successful run and reads nothing itself, so the decision is testable.
/// </summary>
public static class GenerationScopePlanner
{
    public static ScopePlan Plan(
        GenerationScope requested,
        SchemaSnapshot? baseline,
        DatabaseModel current,
        string currentSettingsFingerprint)
    {
        ArgumentNullException.ThrowIfNull(current);

        if (requested == GenerationScope.AllTables)
        {
            return new ScopePlan(GenerationScope.AllTables, null, null);
        }

        if (baseline is null)
        {
            return FallBack("There is no earlier successful generation to compare against");
        }

        if (!string.Equals(baseline.SettingsFingerprint, currentSettingsFingerprint, StringComparison.Ordinal))
        {
            return FallBack("The project's generation settings changed since the last generation, which affects every file");
        }

        var before = baseline.ToDatabaseModel();
        var changed = ChangedTableKeys(before, current, baseline.FailedTables);
        var dropped = SchemaComparer.Compare(before, current).RemovedTables
            .Select(t => t.QualifiedName)
            .ToList();

        if (changed.Count == 0)
        {
            var reason = dropped.Count > 0
                ? $"No remaining table changed structure since the last generation ({Join(dropped)} dropped)"
                : "No table structure changed since the last generation";

            return FallBack(reason);
        }

        var names = current.Tables
            .Where(t => changed.Contains(TableSelection.KeyOf(t)))
            .Select(t => t.QualifiedName)
            .ToList();

        var note = string.Format(
            CultureInfo.InvariantCulture,
            "Generated {0} table{1} changed since the generation of {2:yyyy-MM-dd HH:mm} UTC: {3}.",
            names.Count, names.Count == 1 ? string.Empty : "s", baseline.CapturedUtc, Join(names));

        if (dropped.Count > 0)
        {
            note += $" Dropped since then: {Join(dropped)}; delete their generated files.";
        }

        return new ScopePlan(GenerationScope.ChangedTables, new TableSelection(changed, dropped), note);

        static ScopePlan FallBack(string reason) =>
            new(GenerationScope.AllTables, null, reason + ", so every table was generated.");
    }

    /// <summary>
    /// Tables whose generated code could differ: added tables, tables whose columns or keys
    /// changed, tables whose foreign keys changed (they drive navigation properties and child
    /// loading), and tables the baseline run failed to generate.
    /// </summary>
    public static HashSet<string> ChangedTableKeys(
        DatabaseModel before, DatabaseModel after, IEnumerable<string>? failedTables = null)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);

        var diff = SchemaComparer.Compare(before, after);
        var changed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var table in diff.AddedTables)
        {
            changed.Add(TableSelection.KeyOf(table));
        }

        var afterByName = after.Tables.ToLookup(t => t.Name, StringComparer.OrdinalIgnoreCase);

        foreach (var tableDiff in diff.ChangedTables)
        {
            foreach (var table in afterByName[tableDiff.TableName])
            {
                changed.Add(TableSelection.KeyOf(table));
            }
        }

        var beforeByKey = before.Tables.ToDictionary(TableSelection.KeyOf, StringComparer.OrdinalIgnoreCase);

        foreach (var table in after.Tables)
        {
            if (beforeByKey.TryGetValue(TableSelection.KeyOf(table), out var previous) &&
                !ForeignKeySignature(previous).SetEquals(ForeignKeySignature(table)))
            {
                changed.Add(TableSelection.KeyOf(table));
            }
        }

        var failed = new HashSet<string>(failedTables ?? [], StringComparer.OrdinalIgnoreCase);

        foreach (var table in after.Tables.Where(t => failed.Contains(t.Name)))
        {
            changed.Add(TableSelection.KeyOf(table));
        }

        return changed;
    }

    /// <summary>
    /// A foreign key reduced to what the generated code depends on: its columns and what they
    /// reference. Constraint names are left out, so renaming a constraint changes nothing.
    /// </summary>
    private static HashSet<string> ForeignKeySignature(TableModel table) =>
        table.ForeignKeys
            .Select(f =>
                string.Join(",", f.ForeignKeyMemberColumns.Select(c => c.Name)) + "->" +
                f.PrimaryKeyTableName + "(" + string.Join(",", f.PrimaryKeyMemberColumns.Select(c => c.Name)) + ")")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static string Join(IEnumerable<string> names) => string.Join(", ", names);
}
