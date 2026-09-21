using Voot.CodeGen.Domain.Generation;
using Voot.CodeGen.Domain.Projects;
using Voot.CodeGen.Generation;

namespace Voot.CodeGen.Generation.Tests;

/// <summary>
/// Regressions for defects that only surfaced when the generated output was executed:
/// the T-SQL was run against SQL Server and the C# compiled against the framework contract.
/// </summary>
public class RuntimeRegressionTests
{
    private static GenerationResult Run(OutputStyle style = OutputStyle.Legacy) =>
        new CodeGenerator().Generate(
            TestSchema.Build(),
            new GenerationSettings { RootBase = "HS", TablePrefix = "tbl_", OutputStyle = style });

    private static string File(GenerationResult result, string path) =>
        result.Files.Single(f => f.RelativePath == path).Content;

    [Fact]
    public void Nullable_foreign_keys_are_unwrapped_before_loading_the_related_entity()
    {
        // Customer.CountryId is nullable, so passing it straight to Country's Get(int, bool)
        // does not compile. The generated manager guards on HasValue and passes Value.
        var content = File(Run(), "HS/BusinessLogic/Bases/UserManager.cs");

        Assert.Contains("if (userObject.CountryId.HasValue)", content, StringComparison.Ordinal);
        Assert.Contains("countryManager.Get(userObject.CountryId.Value, fillChilds)", content, StringComparison.Ordinal);
    }

    [Fact]
    public void Non_nullable_foreign_keys_are_passed_without_a_guard()
    {
        // User_Role.UserId is NOT NULL, so no unwrapping is needed.
        var content = File(Run(), "HS/BusinessLogic/Bases/UserManager.cs");

        Assert.DoesNotContain("userObject.CountryId,", content, StringComparison.Ordinal);
    }

    [Fact]
    public void A_guid_key_gets_no_maximum_procedure_or_method()
    {
        // ISNULL(MAX(x), 0) against a uniqueidentifier is an operand type clash, and a GUID
        // has no meaningful maximum, so neither side emits it.
        var result = Run();

        var procedures = File(result, "HS/StoreProcedures/tbl_Role_Procedures.sql");
        Assert.DoesNotContain("MaximumRoleId", procedures, StringComparison.Ordinal);
        Assert.DoesNotContain("ISNULL(MAX", procedures, StringComparison.Ordinal);

        var dataAccess = File(result, "HS/DataAccess/Bases/RoleDataAccess.cs");
        Assert.DoesNotContain("GetMaxRoleId", dataAccess, StringComparison.Ordinal);

        // The row count is unaffected: it does not depend on the key's type.
        Assert.Contains("GetRoleRowCount", procedures, StringComparison.Ordinal);
    }

    [Fact]
    public void A_numeric_key_still_gets_its_maximum_procedure()
    {
        var result = Run();

        Assert.Contains("MaximumUserId", File(result, "HS/StoreProcedures/tbl_User_Procedures.sql"), StringComparison.Ordinal);
        Assert.Contains("public Int32 GetMaxUserId()", File(result, "HS/DataAccess/Bases/UserDataAccess.cs"), StringComparison.Ordinal);
    }

    [Fact]
    public void Row_count_returns_an_int_rather_than_the_key_type()
    {
        // COUNT(*) is an int whatever the key is; returning the key type breaks a GUID-keyed table.
        var content = File(Run(), "HS/DataAccess/Bases/RoleDataAccess.cs");

        Assert.Contains("public int GetRowCount()", content, StringComparison.Ordinal);
    }

    [Fact]
    public void Paging_passes_the_offset_as_a_variable_not_an_expression()
    {
        // sp_executesql rejects an expression as an argument value (Msg 102).
        var content = File(Run(), "HS/StoreProcedures/tbl_User_Procedures.sql");

        Assert.Contains("DECLARE @Skip int = @PageIndex * @RowPerPage;", content, StringComparison.Ordinal);
        Assert.Contains("@Skip = @Skip, @Take = @RowPerPage;", content, StringComparison.Ordinal);
        Assert.DoesNotContain("@Skip = @PageIndex * @RowPerPage", content, StringComparison.Ordinal);
    }

    [Fact]
    public void Select_lists_place_audit_columns_after_the_generated_ones()
    {
        // FillObject reads the generated columns by ordinal and then hands the framework the
        // offset for the audit columns, so the projection order has to match.
        var content = File(Run(), "HS/StoreProcedures/tbl_User_Procedures.sql");
        var selectBlock = content[content.IndexOf("CREATE PROCEDURE [dbo].[GetUserByUserId]", StringComparison.Ordinal)..];

        var userName = selectBlock.IndexOf("[UserName]", StringComparison.Ordinal);
        var creatorId = selectBlock.IndexOf("[CreatorId]", StringComparison.Ordinal);

        Assert.True(userName > 0 && creatorId > userName,
            "audit columns must be projected after the generated ones");
    }

    [Fact]
    public void Both_output_styles_produce_the_same_file_set()
    {
        var legacy = Run(OutputStyle.Legacy).Files.Select(f => f.RelativePath).OrderBy(p => p).ToList();
        var modern = Run(OutputStyle.Modern).Files.Select(f => f.RelativePath).OrderBy(p => p).ToList();

        Assert.Equal(legacy, modern);
    }
}
