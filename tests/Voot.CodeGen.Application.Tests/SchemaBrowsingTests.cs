using Voot.CodeGen.Application.Models;
using Voot.CodeGen.Application.Services;
using Voot.CodeGen.Domain.Schema;

namespace Voot.CodeGen.Application.Tests;

public class SchemaFormatTests
{
    private static ColumnModel Column(
        string nativeType, int size = 0, int precision = 0, int scale = 0, string? defaultValue = null) => new()
        {
            Name = "C",
            Ordinal = 0,
            NativeType = nativeType,
            DataType = SqlDataType.Unknown,
            Size = size,
            Precision = precision,
            Scale = scale,
            DefaultValue = defaultValue
        };

    [Theory]
    [InlineData("int", 0, 0, 0, "int")]
    [InlineData("bit", 0, 0, 0, "bit")]
    [InlineData("nvarchar", 50, 0, 0, "nvarchar(50)")]
    [InlineData("nvarchar", -1, 0, 0, "nvarchar(max)")]
    [InlineData("varbinary", -1, 0, 0, "varbinary(max)")]
    [InlineData("char", 2, 0, 0, "char(2)")]
    [InlineData("decimal", 0, 18, 2, "decimal(18, 2)")]
    [InlineData("datetime2", 0, 0, 0, "datetime2")]
    public void Formats_the_declared_type(string native, int size, int precision, int scale, string expected) =>
        Assert.Equal(expected, SchemaFormat.SqlType(Column(native, size, precision, scale)));

    [Fact]
    public void Shows_an_unmapped_type_rather_than_failing()
    {
        // The generator would reject this type; the browser still has to show it.
        Assert.Equal("geography", SchemaFormat.SqlType(Column("geography")));
    }

    [Theory]
    [InlineData("((0))", "0")]
    [InlineData("(0)", "0")]
    [InlineData("(getutcdate())", "getutcdate()")]
    [InlineData("((1))", "1")]
    [InlineData("(N'x')", "N'x'")]
    [InlineData("(newid())", "newid()")]
    public void Unwraps_the_parentheses_the_catalog_adds(string stored, string expected) =>
        Assert.Equal(expected, SchemaFormat.Default(Column("int", defaultValue: stored)));

    [Fact]
    public void Leaves_a_default_alone_when_the_outer_parentheses_are_not_a_matched_pair()
    {
        // Stripping blindly would turn this into "a) + (b".
        Assert.Equal("(a) + (b)", SchemaFormat.Default(Column("int", defaultValue: "(a) + (b)")));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Reports_no_default_when_there_is_none(string? stored) =>
        Assert.Null(SchemaFormat.Default(Column("int", defaultValue: stored)));
}

public class SchemaSelectionTests
{
    private static TableModel Table(string name) => new()
    {
        Name = name,
        Owner = "dbo",
        Columns = [],
        ForeignKeys = []
    };

    private static readonly DatabaseModel Database = new()
    {
        Name = "Db",
        Tables = [Table("tbl_Country"), Table("tbl_Customer"), Table("tbl_Tag")]
    };

    [Fact]
    public void Selects_nothing_when_nothing_was_picked() =>
        Assert.Empty(SchemaBrowsingService.SelectTables(Database, [], selectAll: false));

    [Fact]
    public void Selects_everything_when_select_all_is_set() =>
        Assert.Equal(3, SchemaBrowsingService.SelectTables(Database, [], selectAll: true).Count);

    [Fact]
    public void Select_all_ignores_the_individual_selection() =>
        Assert.Equal(3, SchemaBrowsingService.SelectTables(Database, ["tbl_Tag"], selectAll: true).Count);

    [Fact]
    public void Matches_table_names_without_regard_to_case()
    {
        var selected = SchemaBrowsingService.SelectTables(Database, ["TBL_customer"], selectAll: false);

        Assert.Single(selected);
        Assert.Equal("tbl_Customer", selected[0].Name);
    }

    [Fact]
    public void Ignores_a_name_that_is_not_in_the_database()
    {
        var selected = SchemaBrowsingService.SelectTables(Database, ["tbl_Gone", "tbl_Tag"], selectAll: false);

        Assert.Single(selected);
        Assert.Equal("tbl_Tag", selected[0].Name);
    }

    [Fact]
    public void Returns_tables_in_database_order_not_selection_order()
    {
        var selected = SchemaBrowsingService.SelectTables(Database, ["tbl_Tag", "tbl_Country"], selectAll: false);

        Assert.Equal(["tbl_Country", "tbl_Tag"], selected.Select(t => t.Name));
    }

    [Fact]
    public void Does_not_duplicate_a_table_named_twice()
    {
        var selected = SchemaBrowsingService.SelectTables(Database, ["tbl_Tag", "tbl_Tag"], selectAll: false);

        Assert.Single(selected);
    }
}
