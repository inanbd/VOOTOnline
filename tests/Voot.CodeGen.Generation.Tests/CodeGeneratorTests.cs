using Voot.CodeGen.Domain.Generation;
using Voot.CodeGen.Domain.Projects;
using Voot.CodeGen.Generation;

namespace Voot.CodeGen.Generation.Tests;

public class CodeGeneratorTests
{
    private static GenerationSettings Settings(OutputStyle style = OutputStyle.Legacy) =>
        new() { RootBase = "HS", TablePrefix = "tbl_", OutputStyle = style, RootNamespace = "http://example.com" };

    private static GenerationResult Run(OutputStyle style = OutputStyle.Legacy) =>
        new CodeGenerator().Generate(TestSchema.Build(), Settings(style));

    private static string File(GenerationResult result, string path) =>
        result.Files.Single(f => f.RelativePath == path).Content;

    [Fact]
    public void Skips_tables_without_a_primary_key()
    {
        var result = Run();

        Assert.Equal(1, result.SkippedTableCount);
        Assert.Contains(result.Diagnostics, d =>
            d.TableName == "tbl_AuditLog" &&
            d.Severity == DiagnosticSeverity.Warning &&
            d.Message.Contains("no primary key", StringComparison.OrdinalIgnoreCase));

        Assert.DoesNotContain(result.Files, f => f.TableName == "tbl_AuditLog");
    }

    [Fact]
    public void Generates_every_expected_file_for_an_ordinary_table()
    {
        var paths = Run().Files.Select(f => f.RelativePath).ToList();

        Assert.Contains("HS/Entities/Bases/UserBase.cs", paths);
        Assert.Contains("HS/Entities/Bases/User.cs", paths);
        Assert.Contains("HS/Entities/User.cs", paths);
        Assert.Contains("HS/Entities/List/UserList.cs", paths);
        Assert.Contains("HS/DataAccess/Bases/UserDataAccess.cs", paths);
        Assert.Contains("HS/DataAccess/UserDataAccess.cs", paths);
        Assert.Contains("HS/BusinessLogic/Bases/UserManager.cs", paths);
        Assert.Contains("HS/BusinessLogic/UserManager.cs", paths);
        Assert.Contains("HS/StoreProcedures/tbl_User_Procedures.sql", paths);
        Assert.Contains("README.txt", paths);
    }

    [Fact]
    public void Strips_the_table_prefix_from_generated_type_names()
    {
        var content = File(Run(), "HS/Entities/Bases/UserBase.cs");

        Assert.Contains("public class UserBase : BaseBusinessEntity", content, StringComparison.Ordinal);

        // The header comment names the source table; no generated *identifier* may carry the prefix.
        var body = content[content.IndexOf("using ", StringComparison.Ordinal)..];
        Assert.DoesNotContain("tbl_", body, StringComparison.Ordinal);
    }

    [Fact]
    public void Junction_tables_use_the_relation_base_and_get_no_manager()
    {
        var result = Run();
        var dataAccess = File(result, "HS/DataAccess/Bases/User_RoleDataAccess.cs");

        Assert.Contains("public partial class User_RoleDataAccess : BaseRelationData", dataAccess, StringComparison.Ordinal);

        // The original generator deliberately produced no business manager for mapping tables.
        Assert.DoesNotContain(result.Files, f => f.RelativePath.Contains("User_RoleManager", StringComparison.Ordinal));
    }

    [Fact]
    public void Ordinary_tables_are_not_treated_as_junction_tables()
    {
        // The original rule tested the raw name for an underscore, so with a tbl_ prefix every
        // table became a mapping table. Stripping the prefix first is what fixes it.
        var dataAccess = File(Run(), "HS/DataAccess/Bases/UserDataAccess.cs");

        Assert.Contains("public partial class UserDataAccess : BaseDataAccess", dataAccess, StringComparison.Ordinal);
    }

    [Fact]
    public void Stored_procedure_names_match_the_constants_the_data_access_layer_calls()
    {
        // This is the defect that made the original output fail at runtime: the DAL stripped the
        // table prefix when building procedure-name constants and the T-SQL template did not.
        var result = Run();
        var dataAccess = File(result, "HS/DataAccess/Bases/UserDataAccess.cs");
        var procedures = File(result, "HS/StoreProcedures/tbl_User_Procedures.sql");

        foreach (var name in new[] { "InsertUser", "UpdateUser", "DeleteUser", "GetUserByUserId", "GetAllUser" })
        {
            Assert.Contains($"\"{name}\"", dataAccess, StringComparison.Ordinal);
            Assert.Contains($"CREATE PROCEDURE [dbo].[{name}]", procedures, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Audit_columns_are_excluded_from_entities_but_declared_on_procedures()
    {
        var result = Run();
        var entity = File(result, "HS/Entities/Bases/UserBase.cs");
        var procedures = File(result, "HS/StoreProcedures/tbl_User_Procedures.sql");

        // The framework base class owns these on the C# side.
        Assert.DoesNotContain("public Int32 CreatorId", entity, StringComparison.Ordinal);
        Assert.DoesNotContain("Property_CreateDate", entity, StringComparison.Ordinal);

        // But the procedures still take them, because the framework supplies the values.
        Assert.Contains("@CreatorId int", procedures, StringComparison.Ordinal);
        Assert.Contains("@CreateDate datetime", procedures, StringComparison.Ordinal);
    }

    [Fact]
    public void Identity_keys_use_an_output_parameter_and_non_identity_keys_do_not()
    {
        var result = Run();

        var user = File(result, "HS/DataAccess/Bases/UserDataAccess.cs");
        Assert.Contains("pInt32Out(UserBase.Property_UserId)", user, StringComparison.Ordinal);

        var role = File(result, "HS/DataAccess/Bases/RoleDataAccess.cs");
        Assert.DoesNotContain("pGuidOut", role, StringComparison.Ordinal);
        Assert.Contains("pGuid(RoleBase.Property_RoleId, roleObject.RoleId)", role, StringComparison.Ordinal);
    }

    [Fact]
    public void Nullable_columns_are_read_through_an_IsDBNull_guard()
    {
        var content = File(Run(), "HS/DataAccess/Bases/UserDataAccess.cs");

        Assert.Contains("if (!reader.IsDBNull(start + 2)) userObject.CountryId", content, StringComparison.Ordinal);
        Assert.Contains("userObject.UserName = reader.GetString( start + 1 );", content, StringComparison.Ordinal);
    }

    [Fact]
    public void Foreign_keys_become_navigation_properties_on_the_entity()
    {
        var content = File(Run(), "HS/Entities/Bases/User.cs");

        Assert.Contains("public Country CountryIdObject", content, StringComparison.Ordinal);
    }

    [Fact]
    public void Legacy_style_emits_wcf_contracts_and_block_namespaces()
    {
        var content = File(Run(OutputStyle.Legacy), "HS/Entities/Bases/UserBase.cs");

        Assert.Contains("using System.Data.SqlClient;", File(Run(), "HS/DataAccess/Bases/UserDataAccess.cs"), StringComparison.Ordinal);
        Assert.Contains("[DataContract(Name = \"UserBase\"", content, StringComparison.Ordinal);
        Assert.Contains("namespace HS.Entities.Bases\n{", content, StringComparison.Ordinal);
        Assert.Contains("private Nullable<Decimal> _Balance;", content, StringComparison.Ordinal);
    }

    [Fact]
    public void Modern_style_drops_wcf_contracts_and_uses_file_scoped_namespaces()
    {
        var result = Run(OutputStyle.Modern);
        var entity = File(result, "HS/Entities/Bases/UserBase.cs");
        var dataAccess = File(result, "HS/DataAccess/Bases/UserDataAccess.cs");

        Assert.Contains("namespace HS.Entities.Bases;", entity, StringComparison.Ordinal);
        Assert.DoesNotContain("[DataContract", entity, StringComparison.Ordinal);
        Assert.DoesNotContain("[DataMember]", entity, StringComparison.Ordinal);
        Assert.Contains("private decimal? _Balance;", entity, StringComparison.Ordinal);
        Assert.Contains("using Microsoft.Data.SqlClient;", dataAccess, StringComparison.Ordinal);
    }

    [Fact]
    public void Decimal_parameters_carry_their_precision()
    {
        var content = File(Run(), "HS/DataAccess/Bases/UserDataAccess.cs");

        Assert.Contains("pDecimal(UserBase.Property_Balance, 18, userObject.Balance)", content, StringComparison.Ordinal);
    }

    [Fact]
    public void Paging_validates_the_sort_column_against_the_real_table()
    {
        var content = File(Run(), "HS/StoreProcedures/tbl_User_Procedures.sql");

        Assert.Contains("IF @SortColumn NOT IN (SELECT name FROM sys.columns", content, StringComparison.Ordinal);
        Assert.Contains("IF UPPER(@SortOrder) <> 'DESC' SET @SortOrder = 'ASC';", content, StringComparison.Ordinal);
    }

    [Fact]
    public void Insert_uses_scope_identity_rather_than_the_global_identity()
    {
        var content = File(Run(), "HS/StoreProcedures/tbl_User_Procedures.sql");

        Assert.Contains("SET @UserId = SCOPE_IDENTITY();", content, StringComparison.Ordinal);
        Assert.DoesNotContain("@@IDENTITY", content, StringComparison.Ordinal);
    }

    [Fact]
    public void Generation_reports_no_errors_for_a_valid_schema()
    {
        var result = Run();

        Assert.False(result.HasErrors);
        Assert.Equal(4, result.TableCount);
    }
}
