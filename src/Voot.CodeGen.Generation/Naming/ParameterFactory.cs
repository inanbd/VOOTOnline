using System.Globalization;
using Voot.CodeGen.Domain.Schema;

namespace Voot.CodeGen.Generation.Naming;

/// <summary>
/// Builds the <c>p*</c> framework helper calls that turn a column into a SqlParameter.
/// Ports <c>GetInParameterMethod</c>, <c>GetOutPrimaryParameterMethod</c> and friends, which
/// the original templates duplicated with slightly different rules in each copy.
/// </summary>
public static class ParameterFactory
{
    /// <summary>
    /// The size argument a sized helper takes, including its trailing comma, or empty when
    /// the helper takes no size or the column is MAX.
    /// </summary>
    private static string SizeArgument(ColumnModel column, SqlTypeInfo info)
    {
        if (!info.ParameterTakesSize)
        {
            return string.Empty;
        }

        if (column.HasPrecision)
        {
            return column.Precision > 0
                ? string.Create(CultureInfo.InvariantCulture, $"{column.Precision}, ")
                : string.Empty;
        }

        // MAX columns pass no size, matching the original templates.
        return column.IsMaxLength || column.Size <= 0
            ? string.Empty
            : string.Create(CultureInfo.InvariantCulture, $"{column.Size}, ");
    }

    /// <summary>
    /// An input parameter, e.g. <c>pNVarChar(UserBase.Property_Name, 50, userObject.Name)</c>.
    /// </summary>
    public static string InParameter(ColumnModel column, string baseClassName, string valueExpression)
    {
        var info = SqlTypeMap.Require(column);
        var property = NameResolver.PropertyName(column);
        return $"{info.ParameterHelper}({baseClassName}.Property_{property}, {SizeArgument(column, info)}{valueExpression})";
    }

    /// <summary>
    /// An input parameter reading from the entity object, e.g. <c>userObject.Name</c>.
    /// </summary>
    public static string InParameterFromObject(ColumnModel column, string baseClassName, string objectVariable) =>
        InParameter(column, baseClassName, $"{objectVariable}.{NameResolver.PropertyName(column)}");

    /// <summary>
    /// An input parameter reading from a local, e.g. <c>_UserId</c>, used by methods that take
    /// a bare key rather than an entity.
    /// </summary>
    public static string InParameterFromLocal(ColumnModel column, string baseClassName) =>
        InParameter(column, baseClassName, $"_{NameResolver.PropertyName(column)}");

    /// <summary>
    /// An output parameter used to receive an identity key after insert, e.g.
    /// <c>pInt32Out(UserBase.Property_UserId)</c>.
    /// </summary>
    public static string OutParameter(ColumnModel column, string baseClassName)
    {
        var info = SqlTypeMap.Require(column);
        var property = NameResolver.PropertyName(column);
        return $"{info.ParameterHelper}Out({baseClassName}.Property_{property})";
    }
}
