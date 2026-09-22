-- Tracks which applied schema changes have reached development and production, and keeps the
-- history of every move. Idempotent: safe to run on every start.

IF COL_LENGTH(N'[dbo].[ChangeRequests]', N'DeployedToDevUtc') IS NULL
BEGIN
    ALTER TABLE [dbo].[ChangeRequests] ADD
        [DeployedToDevUtc]              datetimeoffset(7) NULL,
        [DeployedToDevByUserName]       nvarchar(256)     NULL,
        [DeployedToProductionUtc]       datetimeoffset(7) NULL,
        [DeployedToProductionByUserName] nvarchar(256)    NULL;
END;
GO

-- The append-only record of moves. Kept separate from the change row so the current state and
-- the history of how it got there do not overwrite one another.
IF OBJECT_ID(N'[dbo].[ChangeDeployments]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[ChangeDeployments]
    (
        [Id]               bigint            IDENTITY(1,1) NOT NULL CONSTRAINT [PK_ChangeDeployments] PRIMARY KEY,
        [ChangeRequestId]  uniqueidentifier  NOT NULL,
        [ProjectId]        uniqueidentifier  NOT NULL,
        [Environment]      int               NOT NULL,
        [Action]           int               NOT NULL,
        [MarkedByUserId]   nvarchar(450)     NOT NULL,
        [MarkedByUserName] nvarchar(256)     NULL,
        [MarkedUtc]        datetimeoffset(7) NOT NULL,
        [ChangeTitle]      nvarchar(200)     NULL,
        CONSTRAINT [FK_ChangeDeployments_Changes] FOREIGN KEY ([ChangeRequestId])
            REFERENCES [dbo].[ChangeRequests] ([Id]) ON DELETE CASCADE
    );

    CREATE INDEX [IX_ChangeDeployments_Project] ON [dbo].[ChangeDeployments] ([ProjectId], [MarkedUtc] DESC);
    CREATE INDEX [IX_ChangeDeployments_Change] ON [dbo].[ChangeDeployments] ([ChangeRequestId]);
END;
