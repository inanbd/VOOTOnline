namespace Voot.CodeGen.Generation.Naming;

/// <summary>
/// Where the key of a newly inserted row comes from. The insert procedure and the data access
/// method both branch on this, so they always agree on which side supplies the key.
/// </summary>
public enum InsertKeySource
{
    /// <summary>The caller sets the key and the insert stores it as given.</summary>
    Caller,

    /// <summary>An <c>IDENTITY</c> column: the database always assigns the key.</summary>
    Identity,

    /// <summary>
    /// The key column has a default, such as <c>NEWID()</c>, <c>NEWSEQUENTIALID()</c> or a
    /// sequence. The database assigns the key when the caller leaves it unset and stores the
    /// caller's key otherwise.
    /// </summary>
    ColumnDefault
}
