using Voot.CodeGen.Application.Models;
using Voot.CodeGen.Domain.Generation;

namespace Voot.CodeGen.Application.Tests;

public class PendingScriptBuilderTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 22, 10, 0, 0, TimeSpan.Zero);

    private static ChangeRequest Change(
        string sql, string? title = null, string? user = "admin", int day = 1, string? summary = null) => new()
        {
            ProjectId = Guid.NewGuid(),
            SubmittedByUserId = "u1",
            SubmittedByUserName = user,
            SqlText = sql,
            Title = title,
            StructureSummary = summary,
            Status = ChangeRequestStatus.Applied,
            SubmittedUtc = new DateTimeOffset(2026, 9, day, 8, 0, 0, TimeSpan.Zero),
            AppliedUtc = new DateTimeOffset(2026, 9, day, 8, 0, 0, TimeSpan.Zero)
        };

    [Fact]
    public void Says_so_plainly_when_nothing_is_outstanding()
    {
        var sql = PendingScriptBuilder.Build("Shop", DeploymentEnvironment.Production, [], Now);

        Assert.Contains("Nothing outstanding", sql, StringComparison.Ordinal);
        Assert.Contains("production", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void Names_the_environment_project_and_count()
    {
        var sql = PendingScriptBuilder.Build(
            "Shop", DeploymentEnvironment.Development, [Change("SELECT 1;"), Change("SELECT 2;")], Now);

        Assert.Contains("Pending for development: 2 changes", sql, StringComparison.Ordinal);
        Assert.Contains("-- Project: Shop", sql, StringComparison.Ordinal);
        Assert.Contains("2026-09-22 10:00:00 UTC", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void Uses_the_singular_for_one_change()
    {
        var sql = PendingScriptBuilder.Build("Shop", DeploymentEnvironment.Production, [Change("SELECT 1;")], Now);

        Assert.Contains("1 change\n", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void Keeps_the_order_it_was_given()
    {
        var sql = PendingScriptBuilder.Build(
            "Shop",
            DeploymentEnvironment.Production,
            [Change("ALTER TABLE A ADD X int;", day: 1), Change("ALTER TABLE A ADD Y int;", day: 2)],
            Now);

        Assert.True(
            sql.IndexOf("ADD X", StringComparison.Ordinal) < sql.IndexOf("ADD Y", StringComparison.Ordinal),
            "the earlier change must come first so the script replays correctly");
    }

    [Fact]
    public void Heads_each_change_with_its_origin()
    {
        var sql = PendingScriptBuilder.Build(
            "Shop",
            DeploymentEnvironment.Production,
            [Change("SELECT 1;", title: "Add loyalty points", user: "kaizar", summary: "+1 column")],
            Now);

        Assert.Contains("-- Add loyalty points", sql, StringComparison.Ordinal);
        Assert.Contains("by kaizar", sql, StringComparison.Ordinal);
        Assert.Contains("-- +1 column", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void Separates_changes_with_a_batch_terminator()
    {
        // Without a GO between them, a CREATE PROCEDURE in one script would swallow the next.
        var sql = PendingScriptBuilder.Build(
            "Shop", DeploymentEnvironment.Production, [Change("SELECT 1;"), Change("SELECT 2;")], Now);

        Assert.Equal(2, sql.Split("\nGO\n").Length - 1);
    }

    [Fact]
    public void Does_not_add_a_second_GO_when_the_script_already_ends_with_one()
    {
        var sql = PendingScriptBuilder.Build(
            "Shop", DeploymentEnvironment.Production, [Change("SELECT 1;\nGO")], Now);

        Assert.DoesNotContain("GO\nGO", sql, StringComparison.Ordinal);
    }
}

public class ChangeRequestTrackerTests
{
    private static ChangeRequest Applied() => new()
    {
        ProjectId = Guid.NewGuid(),
        SubmittedByUserId = "u1",
        SqlText = "SELECT 1;",
        Status = ChangeRequestStatus.Applied
    };

    [Fact]
    public void An_applied_change_is_pending_for_both_environments_until_marked()
    {
        var change = Applied();

        Assert.True(change.IsPendingFor(DeploymentEnvironment.Development));
        Assert.True(change.IsPendingFor(DeploymentEnvironment.Production));
    }

    [Fact]
    public void Marking_one_environment_leaves_the_other_outstanding()
    {
        var change = Applied();
        change.DeployedToDevUtc = DateTimeOffset.UtcNow;
        change.DeployedToDevByUserName = "admin";

        Assert.True(change.IsDeployedTo(DeploymentEnvironment.Development));
        Assert.False(change.IsPendingFor(DeploymentEnvironment.Development));
        Assert.Equal("admin", change.DeployedBy(DeploymentEnvironment.Development));

        Assert.False(change.IsDeployedTo(DeploymentEnvironment.Production));
        Assert.True(change.IsPendingFor(DeploymentEnvironment.Production));
    }

    [Fact]
    public void A_failed_change_is_never_pending_because_it_never_ran()
    {
        var change = Applied();
        change.Status = ChangeRequestStatus.Failed;

        Assert.False(change.IsPendingFor(DeploymentEnvironment.Development));
        Assert.False(change.IsPendingFor(DeploymentEnvironment.Production));
    }
}
