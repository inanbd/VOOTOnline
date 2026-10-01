using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Voot.CodeGen.Domain.Generation;
using Voot.CodeGen.Domain.Projects;
using Voot.CodeGen.Domain.Schema;
using Voot.CodeGen.Generation;

namespace Voot.CodeGen.Generation.Tests;

/// <summary>
/// Compiles the generated C# against <see cref="FrameworkContract"/>. Legacy output runs on
/// .NET Framework 4.6 to 4.8, whose projects build at C# 7, so it is compiled at exactly that
/// language version: anything newer, such as an <c>is not</c> pattern, fails here.
/// </summary>
public class CompilationTests
{
    [Fact]
    public void Legacy_output_compiles_as_csharp_7()
    {
        var errors = Compile(OutputStyle.Legacy, LanguageVersion.CSharp7);

        Assert.True(errors.Count == 0, string.Join("\n", errors));
    }

    [Fact]
    public void Modern_output_compiles()
    {
        var errors = Compile(OutputStyle.Modern, LanguageVersion.Latest);

        Assert.True(errors.Count == 0, string.Join("\n", errors));
    }

    private static GenerationResult Generate(OutputStyle style)
    {
        var schema = TestSchema.Build();
        var database = new DatabaseModel { Name = schema.Name, Tables = [.. schema.Tables, EveryTypeTable()] };

        return new CodeGenerator().Generate(
            database,
            new GenerationSettings { RootBase = "HS", TablePrefix = "tbl_", OutputStyle = style, RootNamespace = "http://example.com" });
    }

    private static List<string> Compile(OutputStyle style, LanguageVersion languageVersion)
    {
        var result = Generate(style);
        Assert.False(result.HasErrors);

        var parseOptions = new CSharpParseOptions(languageVersion);
        var sqlClient = style == OutputStyle.Legacy ? "System.Data.SqlClient" : "Microsoft.Data.SqlClient";

        var trees = result.Files
            .Where(f => f.RelativePath.EndsWith(".cs", StringComparison.Ordinal))
            .Select(f => CSharpSyntaxTree.ParseText(f.Content, parseOptions, f.RelativePath))
            .Append(CSharpSyntaxTree.ParseText(FrameworkContract.Source(sqlClient), parseOptions, "FrameworkContract.cs"));

        var compilation = CSharpCompilation.Create(
            $"Generated{style}",
            trees,
            FrameworkReferences(),
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: style == OutputStyle.Modern ? NullableContextOptions.Enable : NullableContextOptions.Disable));

        return [.. compilation.GetDiagnostics()
            .Where(d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error)
            .Select(d => d.ToString())];
    }

    /// <summary>The runtime's own assemblies, without the test's dependencies.</summary>
    private static List<MetadataReference> FrameworkReferences()
    {
        var runtimeDirectory = Path.GetDirectoryName(typeof(object).Assembly.Location)!;

        return [.. ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Where(p => string.Equals(Path.GetDirectoryName(p), runtimeDirectory, StringComparison.Ordinal))
            .Select(p => MetadataReference.CreateFromFile(p))];
    }

    /// <summary>One column of every mapped type, nullable where that changes the emitted C#.</summary>
    private static TableModel EveryTypeTable()
    {
        const string table = "tbl_EveryType";
        var ordinal = 0;

        ColumnModel Of(string nativeType, SqlDataType type, bool nullable = false, int size = 0, int precision = 0, int scale = 0) =>
            TestSchema.Column(table, $"Col{nativeType.Replace("_", string.Empty, StringComparison.Ordinal)}{(nullable ? "Null" : string.Empty)}",
                ordinal++, nativeType, type, nullable: nullable, size: size, precision: precision, scale: scale);

        var key = TestSchema.Column(table, "EveryTypeId", ordinal++, "bigint", SqlDataType.BigInt, identity: true, primaryKey: true);

        List<ColumnModel> columns =
        [
            key,
            Of("binary", SqlDataType.Binary, size: 16),
            Of("bit", SqlDataType.Bit), Of("bit", SqlDataType.Bit, nullable: true),
            Of("char", SqlDataType.Char, size: 1), Of("char", SqlDataType.Char, nullable: true, size: 1),
            Of("date", SqlDataType.Date), Of("datetime", SqlDataType.DateTime), Of("datetime2", SqlDataType.DateTime2),
            Of("smalldatetime", SqlDataType.SmallDateTime), Of("datetimeoffset", SqlDataType.DateTimeOffset, nullable: true),
            Of("decimal", SqlDataType.Decimal, precision: 18, scale: 4), Of("numeric", SqlDataType.Decimal, nullable: true, precision: 9, scale: 2),
            Of("float", SqlDataType.Float), Of("real", SqlDataType.Real, nullable: true),
            Of("image", SqlDataType.Image, nullable: true),
            Of("int", SqlDataType.Int), Of("smallint", SqlDataType.SmallInt, nullable: true), Of("tinyint", SqlDataType.TinyInt),
            Of("money", SqlDataType.Money), Of("smallmoney", SqlDataType.SmallMoney, nullable: true),
            Of("nchar", SqlDataType.NChar, nullable: true, size: 10), Of("ntext", SqlDataType.NText, nullable: true),
            Of("nvarchar", SqlDataType.NVarChar, size: -1), Of("varchar", SqlDataType.VarChar, nullable: true, size: 20),
            Of("text", SqlDataType.Text, nullable: true), Of("sysname", SqlDataType.NVarChar),
            Of("sql_variant", SqlDataType.Variant, nullable: true), Of("xml", SqlDataType.Xml, nullable: true),
            Of("time", SqlDataType.Time, nullable: true), Of("timestamp", SqlDataType.Timestamp),
            Of("uniqueidentifier", SqlDataType.UniqueIdentifier, nullable: true), Of("varbinary", SqlDataType.VarBinary, size: -1)
        ];

        return new TableModel
        {
            Name = table,
            Owner = "dbo",
            Columns = columns,
            PrimaryKey = new PrimaryKeyModel { Name = "PK_EveryType", MemberColumns = [key] },
            ForeignKeys = []
        };
    }
}
