namespace Voot.CodeGen.Generation.Tests;

/// <summary>
/// C# source for a stand-in of the framework assembly the generated code compiles against,
/// plus the handful of ADO.NET types it touches. It declares only the surface the archive
/// README promises, so the generated code is checked against that contract rather than against
/// whatever a real framework happens to offer. It is itself written in C# 7, because it shares
/// a compilation with the Legacy output.
/// </summary>
internal static class FrameworkContract
{
    /// <summary>Every <c>p*</c> helper the type map can emit.</summary>
    private static readonly string[] ParameterHelpers =
    [
        "pBinary", "pBool", "pByte", "pChar", "pDateTime", "pDateTimeOffset", "pDecimal", "pDouble",
        "pGuid", "pImage", "pInt16", "pInt32", "pInt64", "pMoney", "pNChar", "pNVarChar", "pReal",
        "pSmallDateTime", "pText", "pTime", "pTimestamp", "pVarBinary", "pVarChar", "pVariant", "pXml"
    ];

    /// <summary>Helpers that also take a size or precision between the name and the value.</summary>
    private static readonly HashSet<string> SizedHelpers =
        ["pBinary", "pChar", "pDecimal", "pNChar", "pNVarChar", "pVarBinary", "pVarChar"];

    public static string Source(string sqlClientNamespace)
    {
        var helpers = string.Join("\n", ParameterHelpers.SelectMany(HelperDeclarations));

        return $$"""
            using System;
            using System.Collections.Generic;
            using System.Runtime.Serialization;
            using {{sqlClientNamespace}};

            namespace {{sqlClientNamespace}}
            {
                public class SqlCommand : IDisposable { public void Dispose() { } }
                public class SqlParameter { }
                public class SqlTransaction { }
                public class SqlException : Exception { }

                public class SqlDataReader : IDisposable
                {
                    public bool Read() { return false; }
                    public bool IsDBNull(int i) { return true; }
                    public object GetValue(int i) { return null; }
                    public bool GetBoolean(int i) { return false; }
                    public byte GetByte(int i) { return 0; }
                    public short GetInt16(int i) { return 0; }
                    public int GetInt32(int i) { return 0; }
                    public long GetInt64(int i) { return 0; }
                    public float GetFloat(int i) { return 0; }
                    public double GetDouble(int i) { return 0; }
                    public decimal GetDecimal(int i) { return 0; }
                    public string GetString(int i) { return null; }
                    public Guid GetGuid(int i) { return Guid.Empty; }
                    public DateTime GetDateTime(int i) { return DateTime.MinValue; }
                    public DateTimeOffset GetDateTimeOffset(int i) { return DateTimeOffset.MinValue; }
                    public TimeSpan GetTimeSpan(int i) { return TimeSpan.Zero; }
                    public void Close() { }
                    public void Dispose() { }
                }
            }

            // Legacy output imports this for WCF; on .NET Framework it is System.ServiceModel.dll.
            namespace System.ServiceModel
            {
                internal static class Placeholder { }
            }

            namespace HS.Framework
            {
                public class ClientContext { }

                public class PropertyChangingEventArgs : EventArgs
                {
                    public PropertyChangingEventArgs(string propertyName, object newValue, object oldValue) { }
                }

                [Serializable]
                public class BaseBusinessEntity
                {
                    public enum RowStateEnum { NewRow, NormalRow, ModifiedRow, DeletedRow }

                    public RowStateEnum RowState { get; set; }

                    protected bool PropertyChanging(PropertyChangingEventArgs args) { return true; }

                    protected void PropertyChanged(PropertyChangingEventArgs args) { }

                    protected void CloneBase(BaseBusinessEntity target) { }

                    public bool CustomPropertyMatch(BaseBusinessEntity other) { return true; }

                    public virtual void GetObjectData(SerializationInfo info, StreamingContext context) { }
                }

                public class BaseCollection<T> : List<T>
                {
                    public BaseCollection() { }
                    public BaseCollection(T[] list) : base(list) { }
                    public BaseCollection(List<T> list) : base(list) { }
                }

                public class PagedRequest
                {
                    public int PageIndex { get; set; }
                    public int RowPerPage { get; set; }
                    public string WhereClause { get; set; }
                    public string SortColumn { get; set; }
                    public string SortOrder { get; set; }
                    public int TotalRows { get; set; }
                }

                public class BaseManager : IDisposable
                {
                    public BaseManager(ClientContext context) { ClientContext = context; }
                    public BaseManager(SqlTransaction transaction, ClientContext context) { ClientContext = context; }
                    protected ClientContext ClientContext { get; private set; }
                    public void Dispose() { }
                }
            }

            namespace HS.Framework.Exceptions
            {
                public class ObjectInsertException : Exception
                {
                    public ObjectInsertException(object entity, SqlException inner) : base("insert", inner) { }
                }

                public class ObjectUpdateException : Exception
                {
                    public ObjectUpdateException(object entity, SqlException inner) : base("update", inner) { }
                }

                public class ObjectDeleteException : Exception
                {
                    public ObjectDeleteException(Type type, object key, SqlException inner) : base("delete", inner) { }
                }
            }

            namespace HS.Framework.DataAccess
            {
                using HS.Framework;

                public class BaseDataAccess : IDisposable
                {
                    public const long ALL_AVAILABLE_RECORDS = -1;

                    public BaseDataAccess() { }
                    public BaseDataAccess(string connectionString) { }
                    public BaseDataAccess(ClientContext context) { }
                    public BaseDataAccess(SqlTransaction transaction, ClientContext context) { }

                    public void Dispose() { }

                    protected SqlCommand GetSPCommand(string name) { return null; }
                    protected void AddParameter(SqlCommand cmd, SqlParameter parameter) { }
                    protected object GetOutParameter(SqlCommand cmd, string name) { return null; }
                    protected long InsertRecord(SqlCommand cmd) { return 0; }
                    protected long UpdateRecord(SqlCommand cmd) { return 0; }
                    protected long DeleteRecord(SqlCommand cmd) { return 0; }
                    protected object SelectRecords(SqlCommand cmd, out SqlDataReader reader) { reader = null; return null; }
                    protected void FillBaseObject(BaseBusinessEntity entity, SqlDataReader reader, int start) { }

            {{helpers}}
                }

                public class BaseRelationData : BaseDataAccess
                {
                    public BaseRelationData() { }
                    public BaseRelationData(string connectionString) : base(connectionString) { }
                    public BaseRelationData(ClientContext context) : base(context) { }
                    public BaseRelationData(SqlTransaction transaction, ClientContext context) : base(transaction, context) { }
                }
            }
            """;
    }

    private static IEnumerable<string> HelperDeclarations(string helper)
    {
        yield return $"        protected SqlParameter {helper}(string name, object value) {{ return null; }}";

        if (SizedHelpers.Contains(helper))
        {
            yield return $"        protected SqlParameter {helper}(string name, int size, object value) {{ return null; }}";
        }

        yield return $"        protected SqlParameter {helper}Out(string name) {{ return null; }}";
    }
}
