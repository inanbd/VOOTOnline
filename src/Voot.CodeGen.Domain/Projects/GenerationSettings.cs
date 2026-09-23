using Voot.CodeGen.Domain.Generation;

namespace Voot.CodeGen.Domain.Projects;

/// <summary>
/// Per-project generator options. These are the properties the original CodeSmith
/// <c>Generator_AutomatedFish.csp</c> property set carried, plus the output style.
/// Persisted as a JSON blob on the project row so new options do not require a migration.
/// </summary>
public sealed class GenerationSettings
{
    // ---- Naming / namespace layout -------------------------------------------------

    /// <summary>Root namespace segment and top output folder, e.g. <c>HS</c>.</summary>
    public string RootBase { get; set; } = "HS";

    /// <summary>
    /// XML namespace used for WCF <c>[DataContract]</c> attributes, e.g.
    /// <c>http://www.example.com</c>. Only emitted in <see cref="OutputStyle.Legacy"/>.
    /// </summary>
    public string RootNamespace { get; set; } = "http://tempuri.org";

    public string FrameworkBase { get; set; } = "Framework";

    public string EntityProjectBase { get; set; } = "Entities";

    public string EntityBase { get; set; } = "Bases";

    public string BaseReference { get; set; } = "Bases";

    public string EntityListBase { get; set; } = "List";

    public string DataAccessProjectBase { get; set; } = "DataAccess";

    public string BusinessLogicProjectBase { get; set; } = "BusinessLogic";

    public string StoreProcedureBase { get; set; } = "StoreProcedures";

    public string EntityBaseReference { get; set; } = "Bases";

    public string EntityListBaseReference { get; set; } = "List";

    public string DataAccessBaseReference { get; set; } = "Bases";

    public string ExceptionBase { get; set; } = "Exceptions";

    /// <summary>Type name of the ambient context passed to data access and manager constructors.</summary>
    public string ContextName { get; set; } = "ClientContext";

    /// <summary>Stripped from table names when deriving class names, e.g. <c>tbl_</c>.</summary>
    public string TablePrefix { get; set; } = "tbl_";

    // ---- Stored procedure options ---------------------------------------------------

    /// <summary>
    /// Prepended to every generated procedure name, on both the T-SQL side and the
    /// DAL constants. Empty by default.
    /// </summary>
    public string ProcedurePrefix { get; set; } = string.Empty;

    /// <summary>Emit <c>DROP PROCEDURE</c> guards before each <c>CREATE PROCEDURE</c>.</summary>
    public bool IncludeDropStatements { get; set; } = true;

    /// <summary>Optional ORDER BY applied to the select-all procedure.</summary>
    public string OrderByExpression { get; set; } = string.Empty;

    public bool IncludeInsert { get; set; } = true;

    public bool IncludeUpdate { get; set; } = true;

    public bool IncludeDelete { get; set; } = true;

    public bool IncludeSelect { get; set; } = true;

    public bool IncludeSelectAll { get; set; } = true;

    public bool IncludeSelectPaged { get; set; } = true;

    public bool IncludeSelectByForeignKey { get; set; } = true;

    public bool IncludeDeleteByForeignKey { get; set; } = true;

    public bool IncludeSelectByQuery { get; set; } = true;

    public bool IncludeMaxAndRowCount { get; set; } = true;

    // ---- Column handling -------------------------------------------------------------

    /// <summary>
    /// Columns handled by the runtime framework base classes rather than by generated
    /// property/parameter code. Matches the original templates' hard-coded exclusions.
    /// </summary>
    public IList<string> AuditColumns { get; set; } =
        ["CreatorId", "UpdatorId", "CreateDate", "UpdateDate"];

    // ---- Output ----------------------------------------------------------------------

    public OutputStyle OutputStyle { get; set; } = OutputStyle.Legacy;

    /// <summary>
    /// The scope pre-selected when a run is started; each run can still override it. Defaults to
    /// every table so existing projects behave as before.
    /// </summary>
    public GenerationScope DefaultGenerationScope { get; set; } = GenerationScope.AllTables;

    /// <summary>Run the submitted SQL inside a transaction and roll back on failure.</summary>
    public bool UseTransactionForSql { get; set; } = true;

    /// <summary>Shallow copy with an independent audit-column list.</summary>
    public GenerationSettings Clone()
    {
        var copy = (GenerationSettings)MemberwiseClone();
        copy.AuditColumns = [.. AuditColumns];
        return copy;
    }
}
