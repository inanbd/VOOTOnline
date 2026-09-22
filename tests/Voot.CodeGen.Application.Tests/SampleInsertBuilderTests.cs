using Voot.CodeGen.Application.Models;
using Voot.CodeGen.Domain.Schema;

namespace Voot.CodeGen.Application.Tests;

public class SampleInsertBuilderTests
{
    private static ColumnModel Col(
        string name, string type, bool nullable = false, int size = 0, int scale = 0,
        bool identity = false, bool computed = false, bool rowVersion = false, bool fk = false) => new()
        {
            Name = name,
            Ordinal = 0,
            NativeType = type,
            DataType = SqlDataType.Unknown,
            AllowDbNull = nullable,
            Size = size,
            Scale = scale,
            IsIdentity = identity,
            IsComputed = computed,
            IsRowVersion = rowVersion,
            IsForeignKeyMember = fk
        };

    private static TableModel Table(params ColumnModel[] columns) => new()
    {
        Name = "tbl_Thing",
        Owner = "dbo",
        Columns = columns,
        ForeignKeys = []
    };

    [Fact]
    public void Writes_a_statement_targeting_the_qualified_table()
    {
        var sql = SampleInsertBuilder.Build(Table(Col("Name", "nvarchar", size: 50)));

        Assert.Contains("INSERT INTO [dbo].[tbl_Thing]", sql, StringComparison.Ordinal);
        Assert.Contains("[Name]", sql, StringComparison.Ordinal);
        Assert.Contains("VALUES", sql, StringComparison.Ordinal);
        Assert.EndsWith(");\n", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void Omits_columns_the_server_assigns()
    {
        var sql = SampleInsertBuilder.Build(Table(
            Col("Id", "int", identity: true),
            Col("Total", "decimal", computed: true),
            Col("Ver", "timestamp", rowVersion: true),
            Col("Name", "nvarchar", size: 50)));

        Assert.DoesNotContain("[Id]", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("[Total]", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("[Ver]", sql, StringComparison.Ordinal);
        Assert.Contains("[Name]", sql, StringComparison.Ordinal);
        Assert.Contains("-- Omits 3 columns the server assigns (Id, Total, Ver).", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void Warns_when_a_required_foreign_key_needs_a_real_value()
    {
        var sql = SampleInsertBuilder.Build(Table(Col("CountryId", "int", fk: true)));

        Assert.Contains("-- Replace CountryId with keys that exist", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void Leaves_a_nullable_foreign_key_null_so_the_statement_runs()
    {
        var sql = SampleInsertBuilder.Build(Table(Col("CountryId", "int", nullable: true, fk: true)));

        Assert.Contains("NULL", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("-- Replace", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void Handles_a_table_whose_columns_are_all_server_assigned()
    {
        var sql = SampleInsertBuilder.Build(Table(Col("Id", "int", identity: true)));

        Assert.Contains("nothing to insert", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("INSERT INTO", sql, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("int", "1")]
    [InlineData("bigint", "1")]
    [InlineData("bit", "1")]
    [InlineData("money", "1.00")]
    [InlineData("float", "1.0")]
    [InlineData("date", "'2026-01-01'")]
    [InlineData("datetime", "'2026-01-01 09:00:00'")]
    [InlineData("datetimeoffset", "'2026-01-01 09:00:00 +00:00'")]
    [InlineData("time", "'09:00:00'")]
    [InlineData("uniqueidentifier", "NEWID()")]
    [InlineData("varbinary", "0x00")]
    [InlineData("xml", "N'<sample />'")]
    public void Produces_a_literal_appropriate_to_the_type(string type, string expected) =>
        Assert.Equal(expected, SampleInsertBuilder.SampleValue(Col("C", type)));

    [Fact]
    public void Uses_a_unicode_literal_only_for_unicode_columns()
    {
        Assert.Equal("N'Sample'", SampleInsertBuilder.SampleValue(Col("C", "nvarchar", size: 50)));
        Assert.Equal("'Sample'", SampleInsertBuilder.SampleValue(Col("C", "varchar", size: 50)));
    }

    [Fact]
    public void Trims_the_placeholder_to_fit_a_short_column()
    {
        // char(2) would reject 'Sample'.
        Assert.Equal("'Sa'", SampleInsertBuilder.SampleValue(Col("C", "char", size: 2)));
    }

    [Fact]
    public void Matches_the_declared_scale_for_a_decimal()
    {
        Assert.Equal("1.00", SampleInsertBuilder.SampleValue(Col("C", "decimal", scale: 2)));
        Assert.Equal("1.0000", SampleInsertBuilder.SampleValue(Col("C", "decimal", scale: 4)));
        Assert.Equal("1", SampleInsertBuilder.SampleValue(Col("C", "decimal")));
    }

    [Fact]
    public void Falls_back_to_null_for_a_type_it_does_not_know() =>
        Assert.Equal("NULL", SampleInsertBuilder.SampleValue(Col("C", "geography")));

    [Fact]
    public void Builds_one_script_for_several_tables()
    {
        var a = new TableModel { Name = "tbl_A", Owner = "dbo", Columns = [Col("X", "int")], ForeignKeys = [] };
        var b = new TableModel { Name = "tbl_B", Owner = "dbo", Columns = [Col("Y", "int")], ForeignKeys = [] };

        var sql = SampleInsertBuilder.Build([a, b]);

        Assert.Contains("[dbo].[tbl_A]", sql, StringComparison.Ordinal);
        Assert.Contains("[dbo].[tbl_B]", sql, StringComparison.Ordinal);
        Assert.Equal(2, sql.Split("INSERT INTO").Length - 1);
    }
}
