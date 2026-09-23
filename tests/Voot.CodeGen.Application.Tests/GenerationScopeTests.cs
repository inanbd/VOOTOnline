using Voot.CodeGen.Application.Models;
using Voot.CodeGen.Application.Services;
using Voot.CodeGen.Domain.Generation;
using Voot.CodeGen.Domain.Projects;
using Voot.CodeGen.Domain.Schema;
using Voot.CodeGen.Generation;
using Voot.CodeGen.Generation.Tests;

namespace Voot.CodeGen.Application.Tests;

public class GenerationScopeTests
{
    private static readonly DateTimeOffset Captured = new(2026, 9, 1, 10, 30, 0, TimeSpan.Zero);

    private static GenerationSettings Settings() =>
        new() { RootBase = "HS", TablePrefix = "tbl_", RootNamespace = "http://example.com" };

    private static SchemaSnapshot Baseline(DatabaseModel database, GenerationSettings? settings = null, params string[] failed) =>
        SchemaSnapshot.From(database, SettingsFingerprint.Compute(settings ?? Settings()), failed, Captured);

    /// <summary>The test schema with one table swapped for a modified copy.</summary>
    private static DatabaseModel With(DatabaseModel database, string tableName, Func<TableModel, TableModel> change) =>
        new()
        {
            Name = database.Name,
            Tables = [.. database.Tables.Select(t => t.Name == tableName ? change(t) : t)]
        };

    private static DatabaseModel Without(DatabaseModel database, string tableName) =>
        new() { Name = database.Name, Tables = [.. database.Tables.Where(t => t.Name != tableName)] };

    private static TableModel AddColumn(TableModel table, string name, string type = "bit") => new()
    {
        Name = table.Name,
        Owner = table.Owner,
        Columns = [.. table.Columns, TestSchema.Column(table.Name, name, table.Columns.Count, type, SqlDataType.Bit)],
        PrimaryKey = table.PrimaryKey,
        ForeignKeys = table.ForeignKeys
    };

    private static ScopePlan Plan(SchemaSnapshot? baseline, DatabaseModel current, GenerationSettings? settings = null) =>
        GenerationScopePlanner.Plan(
            GenerationScope.ChangedTables, baseline, current, SettingsFingerprint.Compute(settings ?? Settings()));

    [Fact]
    public void All_tables_request_needs_no_baseline()
    {
        var plan = GenerationScopePlanner.Plan(GenerationScope.AllTables, null, TestSchema.Build(), "x");

        Assert.Equal(GenerationScope.AllTables, plan.Scope);
        Assert.Null(plan.Selection);
        Assert.Null(plan.Note);
    }

    [Fact]
    public void Falls_back_to_all_tables_without_a_baseline()
    {
        var plan = Plan(null, TestSchema.Build());

        Assert.Equal(GenerationScope.AllTables, plan.Scope);
        Assert.Null(plan.Selection);
        Assert.Contains("no earlier successful generation", plan.Note);
    }

    [Fact]
    public void Falls_back_to_all_tables_when_settings_changed()
    {
        var current = With(TestSchema.Build(), "tbl_Country", t => AddColumn(t, "IsActive"));
        var changedSettings = Settings();
        changedSettings.RootBase = "Other";

        var plan = Plan(Baseline(TestSchema.Build()), current, changedSettings);

        Assert.Equal(GenerationScope.AllTables, plan.Scope);
        Assert.Contains("settings changed", plan.Note);
    }

    [Fact]
    public void Settings_that_do_not_affect_output_keep_the_fingerprint()
    {
        var a = Settings();
        var b = Settings();
        b.UseTransactionForSql = !a.UseTransactionForSql;
        b.DefaultGenerationScope = GenerationScope.ChangedTables;

        Assert.Equal(SettingsFingerprint.Compute(a), SettingsFingerprint.Compute(b));

        b.OutputStyle = OutputStyle.Modern;
        Assert.NotEqual(SettingsFingerprint.Compute(a), SettingsFingerprint.Compute(b));
    }

    [Fact]
    public void Falls_back_to_all_tables_when_nothing_changed()
    {
        var plan = Plan(Baseline(TestSchema.Build()), TestSchema.Build());

        Assert.Equal(GenerationScope.AllTables, plan.Scope);
        Assert.Contains("No table structure changed", plan.Note);
    }

    [Fact]
    public void Selects_only_the_table_whose_columns_changed()
    {
        var current = With(TestSchema.Build(), "tbl_Country", t => AddColumn(t, "IsActive"));

        var plan = Plan(Baseline(TestSchema.Build()), current);

        Assert.Equal(GenerationScope.ChangedTables, plan.Scope);
        Assert.Equal(["dbo.tbl_Country"], plan.Selection!.TableKeys);
        Assert.Contains("Generated 1 table changed since the generation of 2026-09-01 10:30 UTC: [dbo].[tbl_Country].", plan.Note);
    }

    [Fact]
    public void Selects_an_added_table()
    {
        var baseline = Baseline(Without(TestSchema.Build(), "tbl_Country"));

        var plan = Plan(baseline, TestSchema.Build());

        // tbl_User's foreign key to tbl_Country is new relative to the baseline too.
        Assert.Contains("dbo.tbl_Country", plan.Selection!.TableKeys);
    }

    [Fact]
    public void Selects_a_table_whose_foreign_keys_changed()
    {
        var current = With(TestSchema.Build(), "tbl_User", t => new TableModel
        {
            Name = t.Name,
            Owner = t.Owner,
            Columns = t.Columns,
            PrimaryKey = t.PrimaryKey,
            ForeignKeys = []
        });

        var plan = Plan(Baseline(TestSchema.Build()), current);

        Assert.Equal(["dbo.tbl_User"], plan.Selection!.TableKeys);
    }

    [Fact]
    public void Renaming_a_foreign_key_constraint_changes_nothing()
    {
        var current = With(TestSchema.Build(), "tbl_User", t => new TableModel
        {
            Name = t.Name,
            Owner = t.Owner,
            Columns = t.Columns,
            PrimaryKey = t.PrimaryKey,
            ForeignKeys = [.. t.ForeignKeys.Select(f => new ForeignKeyModel
            {
                Name = f.Name + "_Renamed",
                ForeignKeyTableName = f.ForeignKeyTableName,
                ForeignKeyMemberColumns = f.ForeignKeyMemberColumns,
                PrimaryKeyTableName = f.PrimaryKeyTableName,
                PrimaryKeyMemberColumns = f.PrimaryKeyMemberColumns
            })]
        });

        Assert.Empty(GenerationScopePlanner.ChangedTableKeys(TestSchema.Build(), current));
    }

    [Fact]
    public void Reports_dropped_tables_in_the_note()
    {
        var current = With(Without(TestSchema.Build(), "tbl_AuditLog"), "tbl_Country", t => AddColumn(t, "IsActive"));

        var plan = Plan(Baseline(TestSchema.Build()), current);

        Assert.Equal(GenerationScope.ChangedTables, plan.Scope);
        Assert.Equal(["[dbo].[tbl_AuditLog]"], plan.Selection!.DroppedTables);
        Assert.Contains("Dropped since then: [dbo].[tbl_AuditLog]", plan.Note);
    }

    [Fact]
    public void Retries_tables_that_failed_in_the_baseline_run()
    {
        var current = With(TestSchema.Build(), "tbl_Country", t => AddColumn(t, "IsActive"));

        var plan = Plan(Baseline(TestSchema.Build(), null, "tbl_Role"), current);

        Assert.Equal(["dbo.tbl_Country", "dbo.tbl_Role"], plan.Selection!.TableKeys.Order());
    }

    [Fact]
    public void Snapshot_round_trips_to_identical_generated_output()
    {
        var original = TestSchema.Build();
        var restored = Baseline(original).ToDatabaseModel();

        Assert.False(SchemaComparer.Compare(original, restored).HasChanges);
        Assert.Empty(GenerationScopePlanner.ChangedTableKeys(original, restored));

        var generator = new CodeGenerator();
        var expected = generator.Generate(original, Settings()).Files;
        var actual = generator.Generate(restored, Settings()).Files;

        Assert.Equal(expected.Select(f => f.RelativePath), actual.Select(f => f.RelativePath));

        foreach (var (e, a) in expected.Zip(actual))
        {
            Assert.True(e.Content == a.Content, $"{e.RelativePath} differs:\n{Diff(e.Content, a.Content)}");
        }

        static string Diff(string e, string a)
        {
            var el = e.Split('\n');
            var al = a.Split('\n');
            var i = 0;
            while (i < Math.Min(el.Length, al.Length) && el[i] == al[i]) i++;
            return $"line {i + 1}: expected '{(i < el.Length ? el[i] : "<end>")}' actual '{(i < al.Length ? al[i] : "<end>")}'";
        }
    }

    [Fact]
    public void Snapshot_survives_serialisation()
    {
        var snapshot = Baseline(TestSchema.Build(), null, "tbl_Role");
        var json = System.Text.Json.JsonSerializer.Serialize(snapshot);
        var back = System.Text.Json.JsonSerializer.Deserialize<SchemaSnapshot>(json)!;

        Assert.Equal(snapshot.SettingsFingerprint, back.SettingsFingerprint);
        Assert.Equal(["tbl_Role"], back.FailedTables);
        Assert.False(SchemaComparer.Compare(TestSchema.Build(), back.ToDatabaseModel()).HasChanges);
    }

    [Fact]
    public void Partial_generation_emits_only_the_selected_tables_and_says_so()
    {
        var selection = new TableSelection(["dbo.tbl_User"], ["[dbo].[tbl_Old]"]);

        var result = new CodeGenerator().Generate(TestSchema.Build(), Settings(), selection);

        Assert.True(result.IsPartial);
        Assert.Equal(["[dbo].[tbl_User]"], result.GeneratedTables);
        Assert.All(result.Files.Where(f => f.TableName is not null), f => Assert.Equal("tbl_User", f.TableName));
        Assert.Equal(0, result.SkippedTableCount);

        var readme = result.Files.Single(f => f.RelativePath.EndsWith("README.txt", StringComparison.Ordinal)).Content;
        Assert.Contains("changed tables only", readme);
        Assert.Contains("[dbo].[tbl_Old]", readme);

        // The navigation property to the unselected parent still resolves from the full schema.
        var full = new CodeGenerator().Generate(TestSchema.Build(), Settings());
        foreach (var file in result.Files.Where(f => f.TableName is not null))
        {
            Assert.Equal(full.Files.Single(f => f.RelativePath == file.RelativePath).Content, file.Content);
        }
    }
}
