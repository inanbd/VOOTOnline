using Voot.CodeGen.Domain.Generation;
using Voot.CodeGen.Domain.Projects;
using Voot.CodeGen.Domain.Schema;
using Voot.CodeGen.Generation;
using Voot.CodeGen.Generation.Naming;

namespace Voot.CodeGen.Generation.Tests;

/// <summary>
/// Inserting a row always hands back its key: the procedure returns it, the data access and
/// the manager return it, and the entity carries it. Covers each place a key can come from.
/// </summary>
public class InsertKeyTests
{
    private static GenerationResult Run(OutputStyle style = OutputStyle.Legacy, DatabaseModel? database = null) =>
        new CodeGenerator().Generate(
            database ?? TestSchema.Build(),
            new GenerationSettings { RootBase = "HS", TablePrefix = "tbl_", OutputStyle = style });

    private static string File(GenerationResult result, string path) =>
        result.Files.Single(f => f.RelativePath == path).Content;

    /// <summary>The text of one procedure, from its CREATE to the GO that ends it.</summary>
    private static string Procedure(string script, string name)
    {
        var start = script.IndexOf($"CREATE PROCEDURE [dbo].[{name}]", StringComparison.Ordinal);
        Assert.True(start >= 0, $"{name} not found");
        var end = script.IndexOf("\nGO", start, StringComparison.Ordinal);
        return script[start..end];
    }

    private static string InsertMethod(string dataAccess)
    {
        var start = dataAccess.IndexOf("#region Insert Method", StringComparison.Ordinal);
        var end = dataAccess.IndexOf("#endregion", start, StringComparison.Ordinal);
        return dataAccess[start..end];
    }

    // ---- where the key comes from ----

    [Fact]
    public void Key_source_follows_identity_then_a_usable_default()
    {
        var schema = TestSchema.Build();

        Assert.Equal(InsertKeySource.Identity, NameResolver.InsertKeySourceOf(NameResolver.PrimaryKeyColumn(schema.FindTable("tbl_User")!)));
        Assert.Equal(InsertKeySource.Caller, NameResolver.InsertKeySourceOf(NameResolver.PrimaryKeyColumn(schema.FindTable("tbl_Role")!)));
        Assert.Equal(InsertKeySource.ColumnDefault, NameResolver.InsertKeySourceOf(NameResolver.PrimaryKeyColumn(schema.FindTable("tbl_Document")!)));

        // A string key cannot be read back without a size, so its default is not relied on.
        var code = TestSchema.Column("tbl_Code", "Code", 0, "varchar", SqlDataType.VarChar, primaryKey: true, size: 10, defaultValue: "('X')");
        Assert.Equal(InsertKeySource.Caller, NameResolver.InsertKeySourceOf(code));
    }

    // ---- identity key ----

    [Fact]
    public void Identity_insert_procedure_returns_the_scope_identity()
    {
        var insert = Procedure(File(Run(), "HS/StoreProcedures/tbl_User_Procedures.sql"), "InsertUser");

        Assert.Contains("@UserId int OUTPUT", insert, StringComparison.Ordinal);
        Assert.Contains("SET @UserId = SCOPE_IDENTITY();", insert, StringComparison.Ordinal);
        Assert.Contains("SELECT @UserId AS [UserId];", insert, StringComparison.Ordinal);
        Assert.Contains("RETURN @Result;", insert, StringComparison.Ordinal);
    }

    [Fact]
    public void Identity_insert_returns_the_key_and_sets_it_before_the_row_state()
    {
        var insert = InsertMethod(File(Run(), "HS/DataAccess/Bases/UserDataAccess.cs"));

        Assert.Contains("public Int32 Insert(UserBase userObject)", insert, StringComparison.Ordinal);
        Assert.Contains("return userObject.UserId;", insert, StringComparison.Ordinal);

        // Setting the key raises a property change, which must not leave the new row marked modified.
        var setKey = insert.IndexOf("userObject.UserId = (Int32)GetOutParameter(cmd, UserBase.Property_UserId);", StringComparison.Ordinal);
        var setState = insert.IndexOf("userObject.RowState = BaseBusinessEntity.RowStateEnum.NormalRow;", StringComparison.Ordinal);
        Assert.True(setKey > 0 && setState > setKey, "the key must be set before the row state");
    }

    [Fact]
    public void Modern_insert_returns_the_keyword_key_type()
    {
        var dataAccess = File(Run(OutputStyle.Modern), "HS/DataAccess/Bases/UserDataAccess.cs");
        var manager = File(Run(OutputStyle.Modern), "HS/BusinessLogic/Bases/UserManager.cs");

        Assert.Contains("public int Insert(UserBase userObject)", dataAccess, StringComparison.Ordinal);
        Assert.Contains("public int Insert(User userObject)", manager, StringComparison.Ordinal);
    }

    [Fact]
    public void Junction_tables_return_the_key_too()
    {
        var insert = InsertMethod(File(Run(), "HS/DataAccess/Bases/User_RoleDataAccess.cs"));

        Assert.Contains("public Int32 Insert(User_RoleBase user_RoleObject)", insert, StringComparison.Ordinal);
        Assert.Contains("return user_RoleObject.UserRoleId;", insert, StringComparison.Ordinal);
    }

    // ---- caller-supplied key ----

    [Fact]
    public void Caller_key_is_stored_as_given_and_returned()
    {
        var result = Run();
        var procedure = Procedure(File(result, "HS/StoreProcedures/tbl_Role_Procedures.sql"), "InsertRole");
        var insert = InsertMethod(File(result, "HS/DataAccess/Bases/RoleDataAccess.cs"));

        Assert.Contains("@RoleId uniqueidentifier OUTPUT", procedure, StringComparison.Ordinal);
        Assert.Contains("[RoleId],", procedure, StringComparison.Ordinal);
        Assert.Contains("SELECT @RoleId AS [RoleId];", procedure, StringComparison.Ordinal);
        Assert.DoesNotContain("IS NULL", procedure, StringComparison.Ordinal);

        Assert.Contains("public Guid Insert(RoleBase roleObject)", insert, StringComparison.Ordinal);
        Assert.Contains("AddParameter(cmd, pGuid(RoleBase.Property_RoleId, roleObject.RoleId));", insert, StringComparison.Ordinal);
        Assert.Contains("return roleObject.RoleId;", insert, StringComparison.Ordinal);
    }

    // ---- key filled in by a column default ----

    [Fact]
    public void Defaulted_key_procedure_lets_the_default_assign_an_unset_key()
    {
        var insert = Procedure(File(Run(), "HS/StoreProcedures/tbl_Document_Procedures.sql"), "InsertDocument");

        Assert.Contains("@DocumentId uniqueidentifier = NULL OUTPUT", insert, StringComparison.Ordinal);
        Assert.Contains("IF @DocumentId IS NULL", insert, StringComparison.Ordinal);

        // OUTPUT ... INTO, unlike a bare OUTPUT clause, still works on a table with triggers.
        Assert.Contains("OUTPUT INSERTED.[DocumentId] INTO @InsertedKeys", insert, StringComparison.Ordinal);
        Assert.Contains("SELECT @DocumentId = [DocumentId] FROM @InsertedKeys;", insert, StringComparison.Ordinal);
        Assert.Contains("SELECT @DocumentId AS [DocumentId];", insert, StringComparison.Ordinal);

        // The unset branch leaves the key column out; the other branch stores the caller's key.
        var unset = insert[..insert.IndexOf("ELSE", StringComparison.Ordinal)];
        var supplied = insert[insert.IndexOf("ELSE", StringComparison.Ordinal)..];
        Assert.DoesNotContain("[DocumentId],", unset, StringComparison.Ordinal);
        Assert.Contains("[DocumentId],", supplied, StringComparison.Ordinal);
    }

    [Fact]
    public void Defaulted_key_insert_reads_back_a_key_it_left_unset()
    {
        var insert = InsertMethod(File(Run(), "HS/DataAccess/Bases/DocumentDataAccess.cs"));

        Assert.Contains("public Guid Insert(DocumentBase documentObject)", insert, StringComparison.Ordinal);
        Assert.Contains("bool assignKey = documentObject.DocumentId == default(Guid);", insert, StringComparison.Ordinal);
        Assert.Contains("AddParameter(cmd, pGuidOut(DocumentBase.Property_DocumentId));", insert, StringComparison.Ordinal);
        Assert.Contains("AddParameter(cmd, pGuid(DocumentBase.Property_DocumentId, documentObject.DocumentId));", insert, StringComparison.Ordinal);
        Assert.Contains("documentObject.DocumentId = (Guid)GetOutParameter(cmd, DocumentBase.Property_DocumentId);", insert, StringComparison.Ordinal);
        Assert.Contains("return documentObject.DocumentId;", insert, StringComparison.Ordinal);
    }

    // ---- tables with nothing to write but the key ----

    [Fact]
    public void A_key_only_table_inserts_default_values()
    {
        var markerId = TestSchema.Column("tbl_Marker", "MarkerId", 0, "int", SqlDataType.Int, identity: true, primaryKey: true);
        var tokenId = TestSchema.Column("tbl_Token", "TokenId", 0, "uniqueidentifier", SqlDataType.UniqueIdentifier,
            primaryKey: true, defaultValue: "(newid())");

        var database = new DatabaseModel
        {
            Name = "KeyOnly",
            Tables =
            [
                new TableModel
                {
                    Name = "tbl_Marker", Owner = "dbo", Columns = [markerId], ForeignKeys = [],
                    PrimaryKey = new PrimaryKeyModel { Name = "PK_Marker", MemberColumns = [markerId] }
                },
                new TableModel
                {
                    Name = "tbl_Token", Owner = "dbo", Columns = [tokenId], ForeignKeys = [],
                    PrimaryKey = new PrimaryKeyModel { Name = "PK_Token", MemberColumns = [tokenId] }
                }
            ]
        };

        var result = Run(database: database);
        var marker = Procedure(File(result, "HS/StoreProcedures/tbl_Marker_Procedures.sql"), "InsertMarker");
        var token = Procedure(File(result, "HS/StoreProcedures/tbl_Token_Procedures.sql"), "InsertToken");

        Assert.Contains("INSERT INTO [dbo].[tbl_Marker]\n\tDEFAULT VALUES;", marker, StringComparison.Ordinal);
        Assert.DoesNotContain("VALUES\n", marker, StringComparison.Ordinal);
        Assert.Contains("OUTPUT INSERTED.[TokenId] INTO @InsertedKeys\n\t\tDEFAULT VALUES;", token, StringComparison.Ordinal);
    }

    // ---- the manager ----

    [Fact]
    public void Manager_insert_is_public_and_returns_the_key()
    {
        var manager = File(Run(), "HS/BusinessLogic/Bases/UserManager.cs");

        Assert.Contains("public Int32 Insert(User userObject)", manager, StringComparison.Ordinal);
        Assert.Contains("return data.Insert(userObject);", manager, StringComparison.Ordinal);
        Assert.DoesNotContain("private bool Insert", manager, StringComparison.Ordinal);
    }

    [Fact]
    public void UpdateBase_reports_an_insert_by_the_row_state_rather_than_the_key()
    {
        // A key is not a success flag for every type: a caller-supplied GUID is never zero.
        var manager = File(Run(), "HS/BusinessLogic/Bases/RoleManager.cs");

        Assert.Contains("Insert(roleObject);\n\t\t\t\t\treturn roleObject.RowState == BaseBusinessEntity.RowStateEnum.NormalRow;",
            manager, StringComparison.Ordinal);
    }
}
