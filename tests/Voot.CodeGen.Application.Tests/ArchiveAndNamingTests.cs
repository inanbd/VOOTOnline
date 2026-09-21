using Voot.CodeGen.Application.Services;
using Voot.CodeGen.Infrastructure.Packaging;

namespace Voot.CodeGen.Application.Tests;

public class ArchiveAndNamingTests
{
    [Theory]
    [InlineData("HS/Entities/Bases/UserBase.cs", "HS/Entities/Bases/UserBase.cs")]
    [InlineData("HS\\Entities\\User.cs", "HS/Entities/User.cs")]
    [InlineData("/HS/Entities/User.cs", "HS/Entities/User.cs")]
    [InlineData("HS//Entities///User.cs", "HS/Entities/User.cs")]
    public void Normalizes_archive_entry_paths(string input, string expected) =>
        Assert.Equal(expected, ZipArchiveBuilder.NormalizeEntryPath(input));

    [Theory]
    [InlineData("../../etc/passwd")]
    [InlineData("HS/../../escape.cs")]
    [InlineData("C:/Windows/system32/x.cs")]
    [InlineData("./x/../../y.cs")]
    public void Rejects_archive_paths_that_would_escape_the_root(string input) =>
        Assert.Throws<InvalidOperationException>(() => ZipArchiveBuilder.NormalizeEntryPath(input));

    [Fact]
    public void Rejects_an_empty_archive_path() =>
        Assert.Throws<InvalidOperationException>(() => ZipArchiveBuilder.NormalizeEntryPath("/"));

    [Theory]
    [InlineData("My Project", "My-Project")]
    [InlineData("a/b\\c", "a-b-c")]
    [InlineData("Ünïcode!", "Ünïcode")]   // Unicode letters are kept; only separators and punctuation go
    [InlineData("", "project")]
    [InlineData("///", "project")]
    public void Builds_a_download_name_with_no_path_separators(string projectName, string expectedStem)
    {
        var name = GenerationPipeline.BuildFileName(projectName, new DateTimeOffset(2026, 9, 21, 14, 15, 30, TimeSpan.Zero));

        Assert.StartsWith(expectedStem, name, StringComparison.Ordinal);
        Assert.EndsWith("-20260921-141530.zip", name, StringComparison.Ordinal);
        Assert.DoesNotContain('/', name);
        Assert.DoesNotContain('\\', name);
    }

    [Fact]
    public void Truncates_a_very_long_project_name()
    {
        var name = GenerationPipeline.BuildFileName(new string('x', 200), DateTimeOffset.UtcNow);

        Assert.True(name.Length < 100, $"name was {name.Length} characters");
    }
}
