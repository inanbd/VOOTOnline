using Voot.CodeGen.Domain.Schema;

namespace Voot.CodeGen.Application.Tests;

public class SchemaComparerTests
{
    private static ColumnModel Col(
        string name, string type = "int", bool nullable = false, int size = 0,
        bool identity = false, string? def = null, bool pk = false) => new()
        {
            Name = name,
            Ordinal = 0,
            NativeType = type,
            DataType = SqlDataType.Unknown,
            AllowDbNull = nullable,
            Size = size,
            IsIdentity = identity,
            DefaultValue = def,
            IsPrimaryKeyMember = pk
        };

    private static TableModel Table(string name, params ColumnModel[] columns) => new()
    {
        Name = name,
        Owner = "dbo",
        Columns = columns,
        ForeignKeys = []
    };

    private static DatabaseModel Db(params TableModel[] tables) =>
        new() { Name = "Db", Tables = tables };

    [Fact]
    public void Reports_no_change_for_identical_snapshots()
    {
        var before = Db(Table("tbl_A", Col("Id"), Col("Name", "nvarchar", size: 50)));
        var after = Db(Table("tbl_A", Col("Id"), Col("Name", "nvarchar", size: 50)));

        var diff = SchemaComparer.Compare(before, after);

        Assert.False(diff.HasChanges);
        Assert.Equal("no structural change", diff.Summary());
    }

    [Fact]
    public void Detects_an_added_table()
    {
        var diff = SchemaComparer.Compare(Db(Table("tbl_A", Col("Id"))), Db(Table("tbl_A", Col("Id")), Table("tbl_B", Col("Id"))));

        Assert.Single(diff.AddedTables);
        Assert.Equal("tbl_B", diff.AddedTables[0].Name);
        Assert.Equal("+1 table", diff.Summary());
    }

    [Fact]
    public void Detects_a_dropped_table()
    {
        var diff = SchemaComparer.Compare(Db(Table("tbl_A", Col("Id")), Table("tbl_B", Col("Id"))), Db(Table("tbl_A", Col("Id"))));

        Assert.Single(diff.RemovedTables);
        Assert.Equal("-1 table", diff.Summary());
    }

    [Fact]
    public void Detects_an_added_column()
    {
        var diff = SchemaComparer.Compare(
            Db(Table("tbl_A", Col("Id"))),
            Db(Table("tbl_A", Col("Id"), Col("Extra", "bit"))));

        Assert.Single(diff.ChangedTables);
        Assert.Equal("Extra", diff.ChangedTables[0].AddedColumns[0].Name);
        Assert.Equal("+1 column", diff.Summary());
    }

    [Fact]
    public void Detects_a_dropped_column()
    {
        var diff = SchemaComparer.Compare(
            Db(Table("tbl_A", Col("Id"), Col("Gone"))),
            Db(Table("tbl_A", Col("Id"))));

        Assert.Single(diff.ChangedTables[0].RemovedColumns);
        Assert.Equal("-1 column", diff.Summary());
    }

    [Theory]
    [InlineData("nvarchar", 50, "nvarchar", 100)]   // widened
    [InlineData("int", 0, "bigint", 0)]             // retyped
    public void Detects_an_altered_column(string beforeType, int beforeSize, string afterType, int afterSize)
    {
        var diff = SchemaComparer.Compare(
            Db(Table("tbl_A", Col("C", beforeType, size: beforeSize))),
            Db(Table("tbl_A", Col("C", afterType, size: afterSize))));

        var change = Assert.Single(diff.ChangedTables[0].ChangedColumns);
        Assert.Equal("C", change.ColumnName);
        Assert.NotEqual(change.Before, change.After);
        Assert.Equal("1 column altered", diff.Summary());
    }

    [Fact]
    public void Detects_a_nullability_change()
    {
        var diff = SchemaComparer.Compare(
            Db(Table("tbl_A", Col("C", nullable: true))),
            Db(Table("tbl_A", Col("C", nullable: false))));

        var change = Assert.Single(diff.ChangedTables[0].ChangedColumns);
        Assert.Contains("NULL", change.Before, StringComparison.Ordinal);
        Assert.Contains("NOT NULL", change.After, StringComparison.Ordinal);
    }

    [Fact]
    public void Detects_a_new_default()
    {
        var diff = SchemaComparer.Compare(
            Db(Table("tbl_A", Col("C"))),
            Db(Table("tbl_A", Col("C", def: "((0))"))));

        Assert.Single(diff.ChangedTables[0].ChangedColumns);
    }

    [Fact]
    public void Combines_several_kinds_of_change_in_the_summary()
    {
        var diff = SchemaComparer.Compare(
            Db(Table("tbl_A", Col("Id"), Col("Old"))),
            Db(Table("tbl_A", Col("Id"), Col("New1"), Col("New2")), Table("tbl_B", Col("Id"))));

        Assert.Equal("+1 table, +2 columns, -1 column", diff.Summary());
    }

    [Fact]
    public void Treats_the_same_table_name_in_different_schemas_as_distinct()
    {
        var before = Db(new TableModel { Name = "tbl_A", Owner = "dbo", Columns = [Col("Id")], ForeignKeys = [] });
        var after = Db(new TableModel { Name = "tbl_A", Owner = "sales", Columns = [Col("Id")], ForeignKeys = [] });

        var diff = SchemaComparer.Compare(before, after);

        Assert.Single(diff.AddedTables);
        Assert.Single(diff.RemovedTables);
        Assert.Empty(diff.ChangedTables);
    }

    [Fact]
    public void Matches_table_and_column_names_without_regard_to_case()
    {
        var diff = SchemaComparer.Compare(
            Db(Table("tbl_A", Col("Id"))),
            Db(Table("TBL_a", Col("ID"))));

        Assert.False(diff.HasChanges);
    }
}
