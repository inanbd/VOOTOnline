using Voot.CodeGen.Domain.Generation;
using Voot.CodeGen.Domain.Projects;
using Voot.CodeGen.Generation;

namespace Voot.CodeGen.Generation.Tests;

/// <summary>
/// Covers the column types the original CodeSmith templates could not emit. A nullable
/// uniqueidentifier produced <c>Nullable&lt;Guid&gt;</c> correctly but fell through the
/// parameter switch in some copies, and <c>date</c> had no mapping at all, so both landed
/// <c>__UNKNOWN__</c> in the generated source.
/// </summary>
public class TypeCoverageTests
{
    private static GenerationResult Run(OutputStyle style = OutputStyle.Legacy) =>
        new CodeGenerator().Generate(
            TestSchema.Build(),
            new GenerationSettings { RootBase = "HS", TablePrefix = "tbl_", OutputStyle = style });

    private static string File(GenerationResult result, string path) =>
        result.Files.Single(f => f.RelativePath == path).Content;

    [Fact]
    public void Nothing_in_the_output_is_an_unmapped_type_placeholder()
    {
        foreach (var file in Run().Files)
        {
            Assert.DoesNotContain("__UNKNOWN__", file.Content, StringComparison.Ordinal);
        }
    }

    // ---- nullable uniqueidentifier ----

    [Fact]
    public void Nullable_uniqueidentifier_becomes_a_nullable_guid_property()
    {
        var legacy = File(Run(OutputStyle.Legacy), "HS/Entities/Bases/RoleBase.cs");
        Assert.Contains("private Nullable<Guid> _OwnerId;", legacy, StringComparison.Ordinal);
        Assert.Contains("public Nullable<Guid> OwnerId", legacy, StringComparison.Ordinal);

        var modern = File(Run(OutputStyle.Modern), "HS/Entities/Bases/RoleBase.cs");
        Assert.Contains("private Guid? _OwnerId;", modern, StringComparison.Ordinal);
        Assert.Contains("public Guid? OwnerId", modern, StringComparison.Ordinal);
    }

    [Fact]
    public void Nullable_uniqueidentifier_uses_the_guid_parameter_helper_with_no_size()
    {
        var content = File(Run(), "HS/DataAccess/Bases/RoleDataAccess.cs");

        Assert.Contains("pGuid(RoleBase.Property_OwnerId, roleObject.OwnerId)", content, StringComparison.Ordinal);
    }

    [Fact]
    public void Nullable_uniqueidentifier_is_read_through_an_IsDBNull_guard()
    {
        var content = File(Run(), "HS/DataAccess/Bases/RoleDataAccess.cs");

        // OwnerId sits at ordinal 2 in the projection (key first, then table order).
        Assert.Contains("if (!reader.IsDBNull(start + 2)) roleObject.OwnerId = reader.GetGuid( start + 2 );",
            content, StringComparison.Ordinal);
    }

    [Fact]
    public void Nullable_uniqueidentifier_declares_the_right_procedure_parameter()
    {
        var content = File(Run(), "HS/StoreProcedures/tbl_Role_Procedures.sql");

        Assert.Contains("@OwnerId uniqueidentifier", content, StringComparison.Ordinal);
        Assert.Contains("@ParentRoleId uniqueidentifier", content, StringComparison.Ordinal);
    }

    [Fact]
    public void A_nullable_guid_foreign_key_is_unwrapped_before_loading_its_parent()
    {
        // Passing Nullable<Guid> straight to Get(Guid, bool) does not compile.
        var content = File(Run(), "HS/BusinessLogic/Bases/RoleManager.cs");

        Assert.Contains("if (roleObject.ParentRoleId.HasValue)", content, StringComparison.Ordinal);
        Assert.Contains("roleManager.Get(roleObject.ParentRoleId.Value, fillChilds)", content, StringComparison.Ordinal);
    }

    [Fact]
    public void A_nullable_guid_foreign_key_gets_its_own_lookup()
    {
        var dataAccess = File(Run(), "HS/DataAccess/Bases/RoleDataAccess.cs");
        var procedures = File(Run(), "HS/StoreProcedures/tbl_Role_Procedures.sql");

        Assert.Contains("public RoleList GetByParentRoleId(Nullable<Guid> _ParentRoleId)", dataAccess, StringComparison.Ordinal);
        Assert.Contains("CREATE PROCEDURE [dbo].[GetRoleByParentRoleId]", procedures, StringComparison.Ordinal);
    }

    // ---- date ----

    [Fact]
    public void Date_becomes_a_DateTime_property()
    {
        var legacy = File(Run(OutputStyle.Legacy), "HS/Entities/Bases/RoleBase.cs");
        Assert.Contains("private DateTime _EffectiveFrom;", legacy, StringComparison.Ordinal);
        Assert.Contains("private Nullable<DateTime> _ExpiresOn;", legacy, StringComparison.Ordinal);

        var modern = File(Run(OutputStyle.Modern), "HS/Entities/Bases/RoleBase.cs");
        Assert.Contains("private DateTime _EffectiveFrom;", modern, StringComparison.Ordinal);
        Assert.Contains("private DateTime? _ExpiresOn;", modern, StringComparison.Ordinal);
    }

    [Fact]
    public void Date_uses_the_datetime_parameter_helper_and_reader()
    {
        var content = File(Run(), "HS/DataAccess/Bases/RoleDataAccess.cs");

        Assert.Contains("pDateTime(RoleBase.Property_EffectiveFrom, roleObject.EffectiveFrom)", content, StringComparison.Ordinal);
        Assert.Contains("roleObject.EffectiveFrom = reader.GetDateTime( start + 4 );", content, StringComparison.Ordinal);
    }

    [Fact]
    public void Date_keeps_its_own_type_in_the_procedure_rather_than_widening_to_datetime()
    {
        var content = File(Run(), "HS/StoreProcedures/tbl_Role_Procedures.sql");

        Assert.Contains("@EffectiveFrom date", content, StringComparison.Ordinal);
        Assert.Contains("@ExpiresOn date", content, StringComparison.Ordinal);
        Assert.DoesNotContain("@EffectiveFrom datetime", content, StringComparison.Ordinal);
    }

    // ---- the extra data access constructors ----

    [Fact]
    public void The_partial_data_access_carries_the_extra_constructors()
    {
        var content = File(Run(), "HS/DataAccess/RoleDataAccess.cs");

        Assert.Contains("public RoleDataAccess() { }", content, StringComparison.Ordinal);
        Assert.Contains("public RoleDataAccess(string ConnectionStr) : base(ConnectionStr) { }", content, StringComparison.Ordinal);
    }

    [Fact]
    public void The_regenerated_data_access_does_not_duplicate_them()
    {
        // Both files are the same partial class, so declaring them twice would not compile.
        var content = File(Run(), "HS/DataAccess/Bases/RoleDataAccess.cs");

        Assert.DoesNotContain("public RoleDataAccess() { }", content, StringComparison.Ordinal);
        Assert.DoesNotContain("ConnectionStr", content, StringComparison.Ordinal);
    }

    [Fact]
    public void Junction_tables_get_the_extra_constructors_too()
    {
        var content = File(Run(), "HS/DataAccess/User_RoleDataAccess.cs");

        Assert.Contains("public User_RoleDataAccess() { }", content, StringComparison.Ordinal);
        Assert.Contains("public User_RoleDataAccess(string ConnectionStr) : base(ConnectionStr) { }", content, StringComparison.Ordinal);
    }
}
