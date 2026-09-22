using Voot.CodeGen.Domain.Schema;

namespace Voot.CodeGen.Generation.Tests;

/// <summary>
/// A small synthetic database covering the cases the generator branches on: an identity key,
/// a non-identity key, a nullable foreign key, audit columns, a junction table and a table
/// with no primary key at all.
/// </summary>
public static class TestSchema
{
    public static ColumnModel Column(
        string table,
        string name,
        int ordinal,
        string nativeType,
        SqlDataType type,
        bool nullable = false,
        bool identity = false,
        bool primaryKey = false,
        bool foreignKey = false,
        int size = 0,
        int precision = 0,
        int scale = 0) => new()
        {
            Name = name,
            Ordinal = ordinal,
            NativeType = nativeType,
            DataType = type,
            AllowDbNull = nullable,
            IsIdentity = identity,
            IsPrimaryKeyMember = primaryKey,
            IsForeignKeyMember = foreignKey,
            Size = size,
            Precision = precision,
            Scale = scale,
            TableName = table
        };

    public static DatabaseModel Build()
    {
        // ---- tbl_Country: identity key, one string column ----
        var countryId = Column("tbl_Country", "CountryId", 0, "int", SqlDataType.Int, identity: true, primaryKey: true);
        var countryName = Column("tbl_Country", "Name", 1, "nvarchar", SqlDataType.NVarChar, size: 50);

        var country = new TableModel
        {
            Name = "tbl_Country",
            Owner = "dbo",
            Columns = [countryId, countryName],
            PrimaryKey = new PrimaryKeyModel { Name = "PK_Country", MemberColumns = [countryId] },
            ForeignKeys = []
        };

        // ---- tbl_User: identity key, nullable FK, a decimal, and audit columns ----
        var userId = Column("tbl_User", "UserId", 0, "int", SqlDataType.Int, identity: true, primaryKey: true);
        var userName = Column("tbl_User", "UserName", 1, "nvarchar", SqlDataType.NVarChar, size: 100);
        var userCountryId = Column("tbl_User", "CountryId", 2, "int", SqlDataType.Int, nullable: true, foreignKey: true);
        var balance = Column("tbl_User", "Balance", 3, "decimal", SqlDataType.Decimal, nullable: true, precision: 18, scale: 2);
        var creatorId = Column("tbl_User", "CreatorId", 4, "int", SqlDataType.Int, nullable: true);
        var createDate = Column("tbl_User", "CreateDate", 5, "datetime", SqlDataType.DateTime, nullable: true);

        var user = new TableModel
        {
            Name = "tbl_User",
            Owner = "dbo",
            Columns = [userId, userName, userCountryId, balance, creatorId, createDate],
            PrimaryKey = new PrimaryKeyModel { Name = "PK_User", MemberColumns = [userId] },
            ForeignKeys =
            [
                new ForeignKeyModel
                {
                    Name = "FK_User_Country",
                    ForeignKeyTableName = "tbl_User",
                    ForeignKeyMemberColumns = [userCountryId],
                    PrimaryKeyTableName = "tbl_Country",
                    PrimaryKeyMemberColumns = [countryId]
                }
            ]
        };

        // ---- tbl_Role: non-identity GUID key, plus the types the original templates could
        // not emit: a nullable uniqueidentifier, a nullable GUID foreign key, and date ----
        var roleId = Column("tbl_Role", "RoleId", 0, "uniqueidentifier", SqlDataType.UniqueIdentifier, primaryKey: true);
        var roleName = Column("tbl_Role", "RoleName", 1, "varchar", SqlDataType.VarChar, size: 40);
        var roleOwnerId = Column("tbl_Role", "OwnerId", 2, "uniqueidentifier", SqlDataType.UniqueIdentifier, nullable: true);
        var roleParentId = Column("tbl_Role", "ParentRoleId", 3, "uniqueidentifier", SqlDataType.UniqueIdentifier,
            nullable: true, foreignKey: true);
        var roleEffective = Column("tbl_Role", "EffectiveFrom", 4, "date", SqlDataType.Date);
        var roleExpires = Column("tbl_Role", "ExpiresOn", 5, "date", SqlDataType.Date, nullable: true);

        var role = new TableModel
        {
            Name = "tbl_Role",
            Owner = "dbo",
            Columns = [roleId, roleName, roleOwnerId, roleParentId, roleEffective, roleExpires],
            PrimaryKey = new PrimaryKeyModel { Name = "PK_Role", MemberColumns = [roleId] },
            ForeignKeys =
            [
                new ForeignKeyModel
                {
                    Name = "FK_Role_Parent",
                    ForeignKeyTableName = "tbl_Role",
                    ForeignKeyMemberColumns = [roleParentId],
                    PrimaryKeyTableName = "tbl_Role",
                    PrimaryKeyMemberColumns = [roleId]
                }
            ]
        };

        // ---- tbl_User_Role: junction table (underscore survives prefix stripping) ----
        var mapId = Column("tbl_User_Role", "UserRoleId", 0, "int", SqlDataType.Int, identity: true, primaryKey: true);
        var mapUserId = Column("tbl_User_Role", "UserId", 1, "int", SqlDataType.Int, foreignKey: true);
        var mapRoleId = Column("tbl_User_Role", "RoleId", 2, "uniqueidentifier", SqlDataType.UniqueIdentifier, foreignKey: true);

        var userRole = new TableModel
        {
            Name = "tbl_User_Role",
            Owner = "dbo",
            Columns = [mapId, mapUserId, mapRoleId],
            PrimaryKey = new PrimaryKeyModel { Name = "PK_UserRole", MemberColumns = [mapId] },
            ForeignKeys =
            [
                new ForeignKeyModel
                {
                    Name = "FK_UserRole_User", ForeignKeyTableName = "tbl_User_Role",
                    ForeignKeyMemberColumns = [mapUserId],
                    PrimaryKeyTableName = "tbl_User", PrimaryKeyMemberColumns = [userId]
                },
                new ForeignKeyModel
                {
                    Name = "FK_UserRole_Role", ForeignKeyTableName = "tbl_User_Role",
                    ForeignKeyMemberColumns = [mapRoleId],
                    PrimaryKeyTableName = "tbl_Role", PrimaryKeyMemberColumns = [roleId]
                }
            ]
        };

        // ---- tbl_AuditLog: no primary key, must be skipped ----
        var auditMessage = Column("tbl_AuditLog", "Message", 0, "nvarchar", SqlDataType.NVarChar, size: -1);

        var auditLog = new TableModel
        {
            Name = "tbl_AuditLog",
            Owner = "dbo",
            Columns = [auditMessage],
            PrimaryKey = null,
            ForeignKeys = []
        };

        return new DatabaseModel
        {
            Name = "TestDb",
            Tables = [country, user, role, userRole, auditLog]
        };
    }
}
