using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Voot.CodeGen.Domain.Generation;
using Voot.CodeGen.Domain.Projects;
using Voot.CodeGen.Generation;

namespace Voot.CodeGen.Application.Models;

/// <summary>
/// A hash of the generation settings that affect the generated files, and of the generator's
/// output version. Two runs with the same fingerprint would produce identical output for an
/// unchanged table.
/// </summary>
public static class SettingsFingerprint
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static string Compute(GenerationSettings settings) => Compute(settings, CodeGenerator.OutputVersion);

    internal static string Compute(GenerationSettings settings, int outputVersion)
    {
        ArgumentNullException.ThrowIfNull(settings);

        // Blank out the settings that change how a run behaves but not what it emits, so
        // toggling them does not force the next changed-tables run to regenerate everything.
        var output = settings.Clone();
        output.UseTransactionForSql = true;
        output.DefaultGenerationScope = GenerationScope.AllTables;

        var json = $"{outputVersion}:{JsonSerializer.Serialize(output, Options)}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json))).ToLowerInvariant();
    }
}
