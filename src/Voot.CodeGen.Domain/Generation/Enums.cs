namespace Voot.CodeGen.Domain.Generation;

/// <summary>Which dialect of C# the generator emits.</summary>
public enum OutputStyle
{
    /// <summary>
    /// Faithful to the original CodeSmith templates: <c>System.Data.SqlClient</c>,
    /// WCF <c>[DataContract]</c>/<c>[CollectionDataContract]</c>, block namespaces, no nullable annotations.
    /// </summary>
    Legacy = 0,

    /// <summary>
    /// Modern .NET: <c>Microsoft.Data.SqlClient</c>, file-scoped namespaces, nullable reference
    /// types, no WCF serialization attributes. Same class and method surface as Legacy.
    /// </summary>
    Modern = 1
}

/// <summary>Lifecycle of a single generation run.</summary>
public enum RunStatus
{
    Queued = 0,
    Running = 1,
    Succeeded = 2,
    Failed = 3,
    Cancelled = 4
}

/// <summary>The phase a run was in when a log entry or failure occurred.</summary>
public enum RunStage
{
    Queued = 0,
    ExecutingSql = 1,
    ReadingSchema = 2,
    GeneratingCode = 3,
    PackagingArchive = 4,
    Completed = 5
}

public enum RunLogLevel
{
    Debug = 0,
    Information = 1,
    Warning = 2,
    Error = 3
}

/// <summary>Outcome of a submitted SQL change.</summary>
public enum ChangeRequestStatus
{
    Pending = 0,
    Applied = 1,
    Failed = 2
}

/// <summary>Severity of a per-table generation diagnostic.</summary>
public enum DiagnosticSeverity
{
    Information = 0,
    Warning = 1,
    Error = 2
}

/// <summary>Which tables a generation run produces files for.</summary>
public enum GenerationScope
{
    /// <summary>Every table with a primary key.</summary>
    AllTables = 0,

    /// <summary>
    /// Only tables whose structure changed since the project's last successful generation.
    /// Falls back to every table when there is nothing trustworthy to compare against.
    /// </summary>
    ChangedTables = 1
}
