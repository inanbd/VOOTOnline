-- Lets a run generate only the tables changed since the last generation. Records the scope each
-- run asked for and used, and keeps the schema snapshot of the latest successful run to compare
-- against. Idempotent: safe to run on every start.

IF COL_LENGTH(N'[dbo].[GenerationRuns]', N'RequestedScope') IS NULL
BEGIN
    ALTER TABLE [dbo].[GenerationRuns] ADD
        [RequestedScope] int            NOT NULL CONSTRAINT [DF_Runs_RequestedScope] DEFAULT (0),
        [Scope]          int            NOT NULL CONSTRAINT [DF_Runs_Scope] DEFAULT (0),
        [ScopeNote]      nvarchar(4000) NULL;
END;
GO

-- One row per project in practice: saving a snapshot removes the project's older ones, because
-- only the latest successful run is ever compared against.
IF OBJECT_ID(N'[dbo].[RunSchemaSnapshots]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[RunSchemaSnapshots]
    (
        [RunId]       uniqueidentifier  NOT NULL CONSTRAINT [PK_RunSchemaSnapshots] PRIMARY KEY,
        [ProjectId]   uniqueidentifier  NOT NULL,
        [CapturedUtc] datetimeoffset(7) NOT NULL,
        -- Gzipped JSON.
        [Snapshot]    varbinary(max)    NOT NULL,
        CONSTRAINT [FK_RunSchemaSnapshots_Runs] FOREIGN KEY ([RunId])
            REFERENCES [dbo].[GenerationRuns] ([Id]) ON DELETE CASCADE
    );

    CREATE INDEX [IX_RunSchemaSnapshots_Project] ON [dbo].[RunSchemaSnapshots] ([ProjectId], [CapturedUtc] DESC);
END;
