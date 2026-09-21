-- Application schema for Voot CodeGen.
-- Idempotent: safe to run on every start. Applied once and recorded in __SchemaVersions.

-- ===== ASP.NET Core Identity ==================================================
-- Shaped to match the standard Identity store contracts, but written and read by
-- hand-rolled Dapper stores; there is no Entity Framework in this solution.

IF OBJECT_ID(N'[dbo].[AspNetRoles]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[AspNetRoles]
    (
        [Id]               nvarchar(450) NOT NULL CONSTRAINT [PK_AspNetRoles] PRIMARY KEY,
        [Name]             nvarchar(256) NULL,
        [NormalizedName]   nvarchar(256) NULL,
        [ConcurrencyStamp] nvarchar(max) NULL
    );
    CREATE UNIQUE INDEX [IX_AspNetRoles_NormalizedName]
        ON [dbo].[AspNetRoles] ([NormalizedName]) WHERE [NormalizedName] IS NOT NULL;
END;

IF OBJECT_ID(N'[dbo].[AspNetUsers]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[AspNetUsers]
    (
        [Id]                   nvarchar(450)      NOT NULL CONSTRAINT [PK_AspNetUsers] PRIMARY KEY,
        [UserName]             nvarchar(256)      NULL,
        [NormalizedUserName]   nvarchar(256)      NULL,
        [Email]                nvarchar(256)      NULL,
        [NormalizedEmail]      nvarchar(256)      NULL,
        [EmailConfirmed]       bit                NOT NULL CONSTRAINT [DF_AspNetUsers_EmailConfirmed] DEFAULT (0),
        [PasswordHash]         nvarchar(max)      NULL,
        [SecurityStamp]        nvarchar(max)      NULL,
        [ConcurrencyStamp]     nvarchar(max)      NULL,
        [PhoneNumber]          nvarchar(max)      NULL,
        [PhoneNumberConfirmed] bit                NOT NULL CONSTRAINT [DF_AspNetUsers_PhoneConfirmed] DEFAULT (0),
        [TwoFactorEnabled]     bit                NOT NULL CONSTRAINT [DF_AspNetUsers_TwoFactor] DEFAULT (0),
        [LockoutEnd]           datetimeoffset(7)  NULL,
        [LockoutEnabled]       bit                NOT NULL CONSTRAINT [DF_AspNetUsers_LockoutEnabled] DEFAULT (1),
        [AccessFailedCount]    int                NOT NULL CONSTRAINT [DF_AspNetUsers_AccessFailed] DEFAULT (0),
        [DisplayName]          nvarchar(256)      NULL,
        [IsActive]             bit                NOT NULL CONSTRAINT [DF_AspNetUsers_IsActive] DEFAULT (1),
        [CreatedUtc]           datetimeoffset(7)  NOT NULL CONSTRAINT [DF_AspNetUsers_CreatedUtc] DEFAULT (SYSUTCDATETIME())
    );
    CREATE UNIQUE INDEX [IX_AspNetUsers_NormalizedUserName]
        ON [dbo].[AspNetUsers] ([NormalizedUserName]) WHERE [NormalizedUserName] IS NOT NULL;
    CREATE INDEX [IX_AspNetUsers_NormalizedEmail] ON [dbo].[AspNetUsers] ([NormalizedEmail]);
END;

IF OBJECT_ID(N'[dbo].[AspNetUserRoles]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[AspNetUserRoles]
    (
        [UserId] nvarchar(450) NOT NULL,
        [RoleId] nvarchar(450) NOT NULL,
        CONSTRAINT [PK_AspNetUserRoles] PRIMARY KEY ([UserId], [RoleId]),
        CONSTRAINT [FK_AspNetUserRoles_Users] FOREIGN KEY ([UserId])
            REFERENCES [dbo].[AspNetUsers] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_AspNetUserRoles_Roles] FOREIGN KEY ([RoleId])
            REFERENCES [dbo].[AspNetRoles] ([Id]) ON DELETE CASCADE
    );
END;

IF OBJECT_ID(N'[dbo].[AspNetUserClaims]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[AspNetUserClaims]
    (
        [Id]         int           IDENTITY(1,1) NOT NULL CONSTRAINT [PK_AspNetUserClaims] PRIMARY KEY,
        [UserId]     nvarchar(450) NOT NULL,
        [ClaimType]  nvarchar(max) NULL,
        [ClaimValue] nvarchar(max) NULL,
        CONSTRAINT [FK_AspNetUserClaims_Users] FOREIGN KEY ([UserId])
            REFERENCES [dbo].[AspNetUsers] ([Id]) ON DELETE CASCADE
    );
    CREATE INDEX [IX_AspNetUserClaims_UserId] ON [dbo].[AspNetUserClaims] ([UserId]);
END;

IF OBJECT_ID(N'[dbo].[AspNetUserLogins]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[AspNetUserLogins]
    (
        [LoginProvider]       nvarchar(450) NOT NULL,
        [ProviderKey]         nvarchar(450) NOT NULL,
        [ProviderDisplayName] nvarchar(max) NULL,
        [UserId]              nvarchar(450) NOT NULL,
        CONSTRAINT [PK_AspNetUserLogins] PRIMARY KEY ([LoginProvider], [ProviderKey]),
        CONSTRAINT [FK_AspNetUserLogins_Users] FOREIGN KEY ([UserId])
            REFERENCES [dbo].[AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF OBJECT_ID(N'[dbo].[AspNetUserTokens]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[AspNetUserTokens]
    (
        [UserId]        nvarchar(450) NOT NULL,
        [LoginProvider] nvarchar(450) NOT NULL,
        [Name]          nvarchar(450) NOT NULL,
        [Value]         nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetUserTokens] PRIMARY KEY ([UserId], [LoginProvider], [Name]),
        CONSTRAINT [FK_AspNetUserTokens_Users] FOREIGN KEY ([UserId])
            REFERENCES [dbo].[AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF OBJECT_ID(N'[dbo].[AspNetRoleClaims]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[AspNetRoleClaims]
    (
        [Id]         int           IDENTITY(1,1) NOT NULL CONSTRAINT [PK_AspNetRoleClaims] PRIMARY KEY,
        [RoleId]     nvarchar(450) NOT NULL,
        [ClaimType]  nvarchar(max) NULL,
        [ClaimValue] nvarchar(max) NULL,
        CONSTRAINT [FK_AspNetRoleClaims_Roles] FOREIGN KEY ([RoleId])
            REFERENCES [dbo].[AspNetRoles] ([Id]) ON DELETE CASCADE
    );
    CREATE INDEX [IX_AspNetRoleClaims_RoleId] ON [dbo].[AspNetRoleClaims] ([RoleId]);
END;

-- ===== Projects ===============================================================

IF OBJECT_ID(N'[dbo].[Projects]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[Projects]
    (
        [Id]                        uniqueidentifier  NOT NULL CONSTRAINT [PK_Projects] PRIMARY KEY,
        [Name]                      nvarchar(200)     NOT NULL,
        [Description]               nvarchar(1000)    NULL,
        -- Encrypted with ASP.NET Core Data Protection; never stored or logged in clear.
        [ProtectedConnectionString] nvarchar(max)     NOT NULL,
        [ConnectionStringSummary]   nvarchar(400)     NOT NULL CONSTRAINT [DF_Projects_Summary] DEFAULT (''),
        [SettingsJson]              nvarchar(max)     NOT NULL,
        [CreatedByUserId]           nvarchar(450)     NOT NULL,
        [CreatedUtc]                datetimeoffset(7) NOT NULL,
        [UpdatedUtc]                datetimeoffset(7) NULL,
        [IsActive]                  bit               NOT NULL CONSTRAINT [DF_Projects_IsActive] DEFAULT (1)
    );
    CREATE UNIQUE INDEX [IX_Projects_Name] ON [dbo].[Projects] ([Name]);
END;

IF OBJECT_ID(N'[dbo].[ProjectUsers]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[ProjectUsers]
    (
        [ProjectId]        uniqueidentifier  NOT NULL,
        [UserId]           nvarchar(450)     NOT NULL,
        [AssignedByUserId] nvarchar(450)     NOT NULL,
        [AssignedUtc]      datetimeoffset(7) NOT NULL,
        CONSTRAINT [PK_ProjectUsers] PRIMARY KEY ([ProjectId], [UserId]),
        CONSTRAINT [FK_ProjectUsers_Projects] FOREIGN KEY ([ProjectId])
            REFERENCES [dbo].[Projects] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_ProjectUsers_Users] FOREIGN KEY ([UserId])
            REFERENCES [dbo].[AspNetUsers] ([Id]) ON DELETE CASCADE
    );
    CREATE INDEX [IX_ProjectUsers_UserId] ON [dbo].[ProjectUsers] ([UserId]);
END;

-- ===== Change and run history =================================================
-- Append-mostly: this is the audit trail, so nothing here is deleted with a project.

IF OBJECT_ID(N'[dbo].[ChangeRequests]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[ChangeRequests]
    (
        [Id]                  uniqueidentifier  NOT NULL CONSTRAINT [PK_ChangeRequests] PRIMARY KEY,
        [ProjectId]           uniqueidentifier  NOT NULL,
        [SubmittedByUserId]   nvarchar(450)     NOT NULL,
        [SubmittedByUserName] nvarchar(256)     NULL,
        [Title]               nvarchar(200)     NULL,
        [SqlText]             nvarchar(max)     NOT NULL,
        [Status]              int               NOT NULL,
        [SubmittedUtc]        datetimeoffset(7) NOT NULL,
        [AppliedUtc]          datetimeoffset(7) NULL,
        [BatchesExecuted]     int               NOT NULL CONSTRAINT [DF_ChangeRequests_Batches] DEFAULT (0),
        [RowsAffected]        int               NOT NULL CONSTRAINT [DF_ChangeRequests_Rows] DEFAULT (0),
        [ErrorMessage]        nvarchar(max)     NULL,
        [ErrorNumber]         int               NULL,
        [ErrorLineNumber]     int               NULL
    );
    CREATE INDEX [IX_ChangeRequests_Project] ON [dbo].[ChangeRequests] ([ProjectId], [SubmittedUtc] DESC);
END;

IF OBJECT_ID(N'[dbo].[GenerationRuns]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[GenerationRuns]
    (
        [Id]                  uniqueidentifier  NOT NULL CONSTRAINT [PK_GenerationRuns] PRIMARY KEY,
        [ProjectId]           uniqueidentifier  NOT NULL,
        [ChangeRequestId]     uniqueidentifier  NULL,
        [RequestedByUserId]   nvarchar(450)     NOT NULL,
        [RequestedByUserName] nvarchar(256)     NULL,
        [Status]              int               NOT NULL,
        [Stage]               int               NOT NULL,
        [OutputStyle]         int               NOT NULL,
        [QueuedUtc]           datetimeoffset(7) NOT NULL,
        [StartedUtc]          datetimeoffset(7) NULL,
        [CompletedUtc]        datetimeoffset(7) NULL,
        [TableCount]          int               NOT NULL CONSTRAINT [DF_Runs_TableCount] DEFAULT (0),
        [SkippedTableCount]   int               NOT NULL CONSTRAINT [DF_Runs_Skipped] DEFAULT (0),
        [FileCount]           int               NOT NULL CONSTRAINT [DF_Runs_FileCount] DEFAULT (0),
        [WarningCount]        int               NOT NULL CONSTRAINT [DF_Runs_Warnings] DEFAULT (0),
        [ErrorCount]          int               NOT NULL CONSTRAINT [DF_Runs_Errors] DEFAULT (0),
        [ErrorMessage]        nvarchar(max)     NULL,
        [ErrorDetail]         nvarchar(max)     NULL,
        [ArtifactId]          uniqueidentifier  NULL
    );
    CREATE INDEX [IX_GenerationRuns_Project] ON [dbo].[GenerationRuns] ([ProjectId], [QueuedUtc] DESC);
    CREATE INDEX [IX_GenerationRuns_Status] ON [dbo].[GenerationRuns] ([Status]);
END;

IF OBJECT_ID(N'[dbo].[RunLogEntries]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[RunLogEntries]
    (
        [Id]        bigint            IDENTITY(1,1) NOT NULL CONSTRAINT [PK_RunLogEntries] PRIMARY KEY,
        [RunId]     uniqueidentifier  NOT NULL,
        [LoggedUtc] datetimeoffset(7) NOT NULL,
        [Level]     int               NOT NULL,
        [Stage]     int               NOT NULL,
        [Message]   nvarchar(max)     NOT NULL,
        [Detail]    nvarchar(max)     NULL,
        [TableName] nvarchar(256)     NULL,
        CONSTRAINT [FK_RunLogEntries_Runs] FOREIGN KEY ([RunId])
            REFERENCES [dbo].[GenerationRuns] ([Id]) ON DELETE CASCADE
    );
    CREATE INDEX [IX_RunLogEntries_Run] ON [dbo].[RunLogEntries] ([RunId], [Id]);
END;

IF OBJECT_ID(N'[dbo].[GeneratedArtifacts]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[GeneratedArtifacts]
    (
        [Id]          uniqueidentifier  NOT NULL CONSTRAINT [PK_GeneratedArtifacts] PRIMARY KEY,
        [RunId]       uniqueidentifier  NOT NULL,
        [ProjectId]   uniqueidentifier  NOT NULL,
        [FileName]    nvarchar(260)     NOT NULL,
        [StoragePath] nvarchar(500)     NOT NULL,
        [SizeBytes]   bigint            NOT NULL,
        [Sha256]      nvarchar(64)      NULL,
        [FileCount]   int               NOT NULL CONSTRAINT [DF_Artifacts_FileCount] DEFAULT (0),
        [CreatedUtc]  datetimeoffset(7) NOT NULL,
        [IsAvailable] bit               NOT NULL CONSTRAINT [DF_Artifacts_IsAvailable] DEFAULT (1),
        CONSTRAINT [FK_GeneratedArtifacts_Runs] FOREIGN KEY ([RunId])
            REFERENCES [dbo].[GenerationRuns] ([Id]) ON DELETE CASCADE
    );
    CREATE UNIQUE INDEX [IX_GeneratedArtifacts_Run] ON [dbo].[GeneratedArtifacts] ([RunId]);
    CREATE INDEX [IX_GeneratedArtifacts_Project] ON [dbo].[GeneratedArtifacts] ([ProjectId], [CreatedUtc] DESC);
END;

IF OBJECT_ID(N'[dbo].[GeneratedFiles]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[GeneratedFiles]
    (
        [Id]           bigint           IDENTITY(1,1) NOT NULL CONSTRAINT [PK_GeneratedFiles] PRIMARY KEY,
        [ArtifactId]   uniqueidentifier NOT NULL,
        [RunId]        uniqueidentifier NOT NULL,
        [RelativePath] nvarchar(500)    NOT NULL,
        [Emitter]      nvarchar(100)    NOT NULL,
        [TableName]    nvarchar(256)    NULL,
        [SizeBytes]    int              NOT NULL CONSTRAINT [DF_GeneratedFiles_Size] DEFAULT (0),
        CONSTRAINT [FK_GeneratedFiles_Artifacts] FOREIGN KEY ([ArtifactId])
            REFERENCES [dbo].[GeneratedArtifacts] ([Id]) ON DELETE CASCADE
    );
    CREATE INDEX [IX_GeneratedFiles_Artifact] ON [dbo].[GeneratedFiles] ([ArtifactId]);
END;
