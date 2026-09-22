-- Records what a SQL change did to the schema, so the change history is meaningful without
-- keeping a full before/after snapshot. Idempotent: safe to run on every start.

IF COL_LENGTH(N'[dbo].[ChangeRequests]', N'StructureSummary') IS NULL
BEGIN
    ALTER TABLE [dbo].[ChangeRequests] ADD [StructureSummary] nvarchar(400) NULL;
END;
