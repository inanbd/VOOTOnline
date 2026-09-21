using Voot.CodeGen.Infrastructure.Sql;

namespace Voot.CodeGen.Application.Tests;

public class SqlBatchSplitterTests
{
    [Fact]
    public void Splits_on_a_standalone_GO()
    {
        var batches = SqlBatchSplitter.Split("SELECT 1\nGO\nSELECT 2\nGO\n");

        Assert.Equal(2, batches.Count);
        Assert.Equal("SELECT 1", batches[0]);
        Assert.Equal("SELECT 2", batches[1]);
    }

    [Fact]
    public void Returns_a_single_batch_when_there_is_no_GO()
    {
        var batches = SqlBatchSplitter.Split("ALTER TABLE dbo.T ADD C int NULL;");

        Assert.Single(batches);
    }

    [Fact]
    public void Ignores_empty_batches_and_trailing_separators()
    {
        var batches = SqlBatchSplitter.Split("GO\n\nSELECT 1\nGO\nGO\n   \nGO");

        Assert.Single(batches);
        Assert.Equal("SELECT 1", batches[0]);
    }

    [Fact]
    public void Accepts_a_repeat_count_and_a_trailing_comment_after_GO()
    {
        Assert.Equal(2, SqlBatchSplitter.Split("SELECT 1\nGO 5\nSELECT 2").Count);
        Assert.Equal(2, SqlBatchSplitter.Split("SELECT 1\nGO -- run it\nSELECT 2").Count);
    }

    [Fact]
    public void Does_not_split_on_GO_inside_a_string_literal()
    {
        var sql = "INSERT INTO T VALUES ('line one\nGO\nline two');\nGO\nSELECT 2";
        var batches = SqlBatchSplitter.Split(sql);

        Assert.Equal(2, batches.Count);
        Assert.Contains("line two", batches[0], StringComparison.Ordinal);
    }

    [Fact]
    public void Does_not_split_on_GO_inside_a_block_comment()
    {
        var batches = SqlBatchSplitter.Split("SELECT 1\n/* note\nGO\nstill a comment */\nSELECT 2\nGO");

        Assert.Single(batches);
    }

    [Fact]
    public void Does_not_split_on_GO_inside_a_bracketed_identifier()
    {
        var batches = SqlBatchSplitter.Split("SELECT [weird\nGO\ncolumn] FROM T\nGO\nSELECT 2");

        Assert.Equal(2, batches.Count);
    }

    [Fact]
    public void Treats_a_doubled_quote_as_an_escape_rather_than_a_terminator()
    {
        // The '' keeps the parser inside the literal, so the GO on the next line is data.
        var batches = SqlBatchSplitter.Split("INSERT INTO T VALUES ('it''s\nGO\nfine');\nGO\nSELECT 2");

        Assert.Equal(2, batches.Count);
    }

    [Fact]
    public void Does_not_split_on_a_word_that_merely_starts_with_GO()
    {
        var batches = SqlBatchSplitter.Split("SELECT 1\nGOTO done\nSELECT 2");

        Assert.Single(batches);
    }

    [Fact]
    public void Is_case_insensitive_and_tolerates_surrounding_whitespace()
    {
        Assert.Equal(2, SqlBatchSplitter.Split("SELECT 1\n   go   \nSELECT 2").Count);
    }

    [Fact]
    public void Returns_nothing_for_blank_input()
    {
        Assert.Empty(SqlBatchSplitter.Split("   \n  \n"));
        Assert.Empty(SqlBatchSplitter.Split(""));
    }
}
