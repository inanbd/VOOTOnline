namespace Voot.CodeGen.Domain.Schema;

/// <summary>
/// The SQL Server native types the generator understands. Mirrors <c>System.Data.SqlDbType</c>
/// but is declared here so the Domain layer stays free of data-provider dependencies.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Naming", "CA1720:Identifier contains type name",
    Justification = "Members deliberately mirror SQL Server's native type names.")]
public enum SqlDataType
{
    Unknown = 0,
    BigInt,
    Binary,
    Bit,
    Char,
    Date,
    DateTime,
    DateTime2,
    DateTimeOffset,
    Decimal,
    Float,
    Geography,
    Geometry,
    HierarchyId,
    Image,
    Int,
    Money,
    NChar,
    NText,
    NVarChar,
    Real,
    SmallDateTime,
    SmallInt,
    SmallMoney,
    Text,
    Time,
    Timestamp,
    TinyInt,
    UniqueIdentifier,
    VarBinary,
    VarChar,
    Variant,
    Xml
}
