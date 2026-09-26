using Voot.CodeGen.Application.Models;
using Voot.CodeGen.Application.Services;

namespace Voot.CodeGen.Application.Tests;

public class ProjectSetupRulesTests
{
    [Theory]
    [InlineData("SalesDev")]
    [InlineData("a")]
    [InlineData("Shop_2026_dev")]
    public void Accepts_plain_database_names(string name) =>
        Assert.Null(DatabaseNameRule.Validate(name));

    [Theory]
    [InlineData(null, "Enter a name")]
    [InlineData("  ", "Enter a name")]
    [InlineData("2Shop", "start with a letter")]
    [InlineData("_Shop", "start with a letter")]
    [InlineData("Shop Dev", "letters, digits and underscores")]
    [InlineData("Shop];DROP DATABASE x;--", "letters, digits and underscores")]
    [InlineData("Shop-Dev", "letters, digits and underscores")]
    [InlineData("master", "system database")]
    [InlineData("TempDB", "system database")]
    public void Rejects_unsafe_or_reserved_database_names(string? name, string expected) =>
        Assert.Contains(expected, DatabaseNameRule.Validate(name));

    [Fact]
    public void Rejects_names_over_the_length_limit() =>
        Assert.Contains("at most", DatabaseNameRule.Validate(new string('a', DatabaseNameRule.MaxLength + 1)));

    [Fact]
    public void Finds_the_use_statement_management_studio_exports_start_with()
    {
        const string script = """
            USE [OldShop]
            GO
            /****** Object:  Table [dbo].[tbl_Order] ******/
            SET ANSI_NULLS ON
            GO
            CREATE TABLE [dbo].[tbl_Order] ([OrderId] int NOT NULL)
            GO
            """;

        var issue = Assert.Single(SqlScriptInspector.FindDatabaseLevelStatements(script));

        Assert.Equal(1, issue.Line);
        Assert.Equal("USE [OldShop]", issue.Text);
    }

    [Fact]
    public void Finds_database_level_ddl_with_line_numbers()
    {
        const string script = "CREATE TABLE t (Id int)\n" +
            "go\n" +
            "create   database Other\n" +
            "ALTER DATABASE [Other] SET RECOVERY SIMPLE\n" +
            "DROP DATABASE Other;";

        var issues = SqlScriptInspector.FindDatabaseLevelStatements(script);

        Assert.Equal([3, 4, 5], issues.Select(i => i.Line));
    }

    [Theory]
    [InlineData("-- USE [Other]\nCREATE TABLE t (Id int)")]
    [InlineData("/* USE Other\n /* nested */ DROP DATABASE x */ CREATE TABLE t (Id int)")]
    [InlineData("INSERT INTO t (Note) VALUES ('USE the other one; DROP DATABASE x')")]
    [InlineData("CREATE TABLE t ([USE] int, \"Database\" int, UserId int, UsedOn date)")]
    [InlineData("DECLARE @use int = 1; SELECT @use;")]
    [InlineData("ALTER DATABASE SCOPED CONFIGURATION SET MAXDOP = 1")]
    [InlineData("ALTER DATABASE CURRENT SET RECOVERY SIMPLE")]
    [InlineData("CREATE DATABASE SCOPED CREDENTIAL c WITH IDENTITY = 'x'")]
    [InlineData("CREATE DATABASE ENCRYPTION KEY WITH ALGORITHM = AES_256 ENCRYPTION BY SERVER CERTIFICATE c")]
    [InlineData("SELECT 'it''s USE' AS [a]]USE]")]
    public void Ignores_keywords_in_comments_strings_identifiers_and_scoped_statements(string script) =>
        Assert.Empty(SqlScriptInspector.FindDatabaseLevelStatements(script));
}
