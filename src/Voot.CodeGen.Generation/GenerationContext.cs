using Voot.CodeGen.Domain.Generation;
using Voot.CodeGen.Domain.Projects;
using Voot.CodeGen.Domain.Schema;
using Voot.CodeGen.Generation.Naming;

namespace Voot.CodeGen.Generation;

/// <summary>
/// Everything an emitter needs for one run: the schema snapshot, the project's settings,
/// the resolved naming rules, and the namespace and folder layout derived from them.
/// </summary>
public sealed class GenerationContext(DatabaseModel database, GenerationSettings settings)
{
    public DatabaseModel Database { get; } = database;

    public GenerationSettings Settings { get; } = settings;

    public NameResolver Names { get; } = new(settings);

    public OutputStyle Style => Settings.OutputStyle;

    public bool IsLegacy => Style == OutputStyle.Legacy;

    public bool IsModern => Style == OutputStyle.Modern;

    /// <summary>Legacy output used tabs, matching the original templates; modern output uses spaces.</summary>
    public CodeWriter NewWriter() => new(IsLegacy ? "\t" : "    ");

    // ---- Namespaces --------------------------------------------------------------------

    public string FrameworkNamespace => $"{Settings.RootBase}.{Settings.FrameworkBase}";

    public string FrameworkDataAccessNamespace =>
        $"{Settings.RootBase}.{Settings.FrameworkBase}.{Settings.DataAccessProjectBase}";

    public string ExceptionsNamespace =>
        $"{Settings.RootBase}.{Settings.FrameworkBase}.{Settings.ExceptionBase}";

    public string EntitiesNamespace => $"{Settings.RootBase}.{Settings.EntityProjectBase}";

    public string EntityBaseNamespace => string.IsNullOrEmpty(Settings.EntityProjectBase)
        ? $"{Settings.RootBase}.{Settings.EntityBase}"
        : $"{Settings.RootBase}.{Settings.EntityProjectBase}.{Settings.EntityBase}";

    public string EntityBaseReferenceNamespace =>
        $"{Settings.RootBase}.{Settings.EntityProjectBase}.{Settings.EntityBaseReference}";

    public string EntityListNamespace => string.IsNullOrEmpty(Settings.EntityListBase)
        ? $"{Settings.RootBase}.{Settings.EntityProjectBase}"
        : $"{Settings.RootBase}.{Settings.EntityProjectBase}.{Settings.EntityListBase}";

    public string EntityListReferenceNamespace =>
        $"{Settings.RootBase}.{Settings.EntityProjectBase}.{Settings.EntityListBaseReference}";

    public string DataAccessNamespace => $"{Settings.RootBase}.{Settings.DataAccessProjectBase}";

    public string BusinessLogicNamespace => $"{Settings.RootBase}.{Settings.BusinessLogicProjectBase}";

    /// <summary>XML namespace for WCF data contracts; only used by the legacy style.</summary>
    public string ContractNamespace => Settings.RootNamespace.TrimEnd('/');

    // ---- Output folders ----------------------------------------------------------------
    // Files under a "Bases" folder are fully regenerated; files in the parent folder are the
    // hand-editable partials, which is why the two sets live apart.

    public string EntityBaseFolder =>
        $"{Settings.RootBase}/{Settings.EntityProjectBase}/{Settings.EntityBase}";

    public string EntityFolder => $"{Settings.RootBase}/{Settings.EntityProjectBase}";

    public string EntityListFolder =>
        $"{Settings.RootBase}/{Settings.EntityProjectBase}/{Settings.EntityListBase}";

    public string DataAccessBaseFolder =>
        $"{Settings.RootBase}/{Settings.DataAccessProjectBase}/{Settings.BaseReference}";

    public string DataAccessFolder => $"{Settings.RootBase}/{Settings.DataAccessProjectBase}";

    public string BusinessLogicBaseFolder =>
        $"{Settings.RootBase}/{Settings.BusinessLogicProjectBase}/{Settings.BaseReference}";

    public string BusinessLogicFolder => $"{Settings.RootBase}/{Settings.BusinessLogicProjectBase}";

    public string StoredProcedureFolder => $"{Settings.RootBase}/{Settings.StoreProcedureBase}";

    // ---- Style helpers ------------------------------------------------------------------

    /// <summary>The ADO.NET namespace appropriate to the output style.</summary>
    public string SqlClientNamespace => IsLegacy ? "System.Data.SqlClient" : "Microsoft.Data.SqlClient";

    /// <summary>
    /// Writes the namespace declaration and returns a scope. Legacy emits a braced block;
    /// modern emits a file-scoped declaration and an empty scope.
    /// </summary>
    public IDisposable NamespaceScope(CodeWriter writer, string namespaceName)
    {
        if (IsLegacy)
        {
            return writer.Block($"namespace {namespaceName}");
        }

        writer.Line($"namespace {namespaceName};");
        writer.Blank();
        return NullScope.Instance;
    }

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();

        public void Dispose()
        {
            // File-scoped namespaces need no closing brace.
        }
    }
}
