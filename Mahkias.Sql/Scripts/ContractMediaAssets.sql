-- Contract step. Drops legacy file columns only after every document has an asset.
-- Run Scripts/AddMediaAssets.sql and Scripts/AddProjectDocumentAssets.sql again
-- immediately before this script so files saved by the previous API during the
-- rollout are copied first. Then run Scripts/VerifyMediaAssets.sql.
-- This script does not delete document rows or media.assets rows.
SET NOCOUNT ON;
GO

IF COL_LENGTH(N'projects.QuotationDocuments', N'FileName') IS NOT NULL
   AND EXISTS (SELECT 1 FROM [projects].[QuotationDocuments] WHERE AssetId IS NULL)
BEGIN
    THROW 50030, N'Quotation documents are still unlinked. Re-run Scripts/AddMediaAssets.sql. Legacy columns were not dropped.', 1;
END
GO

IF COL_LENGTH(N'projects.PurchaseOrderDocuments', N'FileName') IS NOT NULL
   AND EXISTS (SELECT 1 FROM [projects].[PurchaseOrderDocuments] WHERE AssetId IS NULL)
BEGIN
    THROW 50031, N'Purchase order documents are still unlinked. Re-run Scripts/AddMediaAssets.sql. Legacy columns were not dropped.', 1;
END
GO

IF COL_LENGTH(N'projects.ProjectDocuments', N'FileName') IS NOT NULL
   AND EXISTS (SELECT 1 FROM [projects].[ProjectDocuments] WHERE AssetId IS NULL)
BEGIN
    THROW 50032, N'Project documents are still unlinked. Re-run Scripts/AddProjectDocumentAssets.sql. Legacy columns were not dropped.', 1;
END
GO

IF COL_LENGTH(N'projects.QuotationDocuments', N'FileName') IS NOT NULL
BEGIN
    IF EXISTS (SELECT 1 FROM [projects].[QuotationDocuments] WHERE AssetId IS NULL)
    BEGIN
        THROW 50030, N'Quotation documents are still unlinked. Legacy columns were not dropped.', 1;
    END

    IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_QuotationDocuments_QuotationId' AND object_id = OBJECT_ID(N'projects.QuotationDocuments'))
    BEGIN
        DROP INDEX [IX_QuotationDocuments_QuotationId] ON [projects].[QuotationDocuments];
    END

    DECLARE @dropQuotation nvarchar(max) = N'';
    SELECT @dropQuotation += N'ALTER TABLE [projects].[QuotationDocuments] DROP CONSTRAINT ' + QUOTENAME(dc.name) + N';'
    FROM sys.default_constraints AS dc
    INNER JOIN sys.columns AS c ON c.default_object_id = dc.object_id
    WHERE dc.parent_object_id = OBJECT_ID(N'projects.QuotationDocuments')
      AND c.name = N'UploadedAt';
    IF @dropQuotation <> N'' EXEC sp_executesql @dropQuotation;

    EXEC sys.sp_executesql N'ALTER TABLE [projects].[QuotationDocuments] DROP COLUMN [FileName], [FileType], [GoogleDriveFileId], [WebViewLink], [UploadedAt];';
    ALTER TABLE [projects].[QuotationDocuments] ALTER COLUMN [AssetId] UNIQUEIDENTIFIER NOT NULL;
END
GO

IF COL_LENGTH(N'projects.PurchaseOrderDocuments', N'FileName') IS NOT NULL
BEGIN
    IF EXISTS (SELECT 1 FROM [projects].[PurchaseOrderDocuments] WHERE AssetId IS NULL)
    BEGIN
        THROW 50031, N'Purchase order documents are still unlinked. Legacy columns were not dropped.', 1;
    END

    IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_PurchaseOrderDocuments_PurchaseOrderId' AND object_id = OBJECT_ID(N'projects.PurchaseOrderDocuments'))
    BEGIN
        DROP INDEX [IX_PurchaseOrderDocuments_PurchaseOrderId] ON [projects].[PurchaseOrderDocuments];
    END

    DECLARE @dropOrder nvarchar(max) = N'';
    SELECT @dropOrder += N'ALTER TABLE [projects].[PurchaseOrderDocuments] DROP CONSTRAINT ' + QUOTENAME(dc.name) + N';'
    FROM sys.default_constraints AS dc
    INNER JOIN sys.columns AS c ON c.default_object_id = dc.object_id
    WHERE dc.parent_object_id = OBJECT_ID(N'projects.PurchaseOrderDocuments')
      AND c.name = N'UploadedAt';
    IF @dropOrder <> N'' EXEC sp_executesql @dropOrder;

    EXEC sys.sp_executesql N'ALTER TABLE [projects].[PurchaseOrderDocuments] DROP COLUMN [FileName], [FileType], [GoogleDriveFileId], [WebViewLink], [UploadedAt];';
    ALTER TABLE [projects].[PurchaseOrderDocuments] ALTER COLUMN [AssetId] UNIQUEIDENTIFIER NOT NULL;
END
GO

IF COL_LENGTH(N'projects.ProjectDocuments', N'FileName') IS NOT NULL
BEGIN
    IF EXISTS (SELECT 1 FROM [projects].[ProjectDocuments] WHERE AssetId IS NULL)
    BEGIN
        THROW 50032, N'Project documents are still unlinked. Legacy columns were not dropped.', 1;
    END

    IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ProjectDocuments_ProjectId' AND object_id = OBJECT_ID(N'projects.ProjectDocuments'))
    BEGIN
        DROP INDEX [IX_ProjectDocuments_ProjectId] ON [projects].[ProjectDocuments];
    END

    DECLARE @dropProject nvarchar(max) = N'';
    SELECT @dropProject += N'ALTER TABLE [projects].[ProjectDocuments] DROP CONSTRAINT ' + QUOTENAME(dc.name) + N';'
    FROM sys.default_constraints AS dc
    INNER JOIN sys.columns AS c ON c.default_object_id = dc.object_id
    WHERE dc.parent_object_id = OBJECT_ID(N'projects.ProjectDocuments')
      AND c.name = N'UploadedAt';
    IF @dropProject <> N'' EXEC sp_executesql @dropProject;

    EXEC sys.sp_executesql N'ALTER TABLE [projects].[ProjectDocuments] DROP COLUMN [FileName], [FileType], [GoogleDriveFileId], [WebViewLink], [UploadedAt];';
    ALTER TABLE [projects].[ProjectDocuments] ALTER COLUMN [AssetId] UNIQUEIDENTIFIER NOT NULL;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_QuotationDocuments_Assets')
BEGIN
    ALTER TABLE [projects].[QuotationDocuments]
    ADD CONSTRAINT [FK_QuotationDocuments_Assets] FOREIGN KEY ([AssetId]) REFERENCES [media].[assets] ([id]);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_PurchaseOrderDocuments_Assets')
BEGIN
    ALTER TABLE [projects].[PurchaseOrderDocuments]
    ADD CONSTRAINT [FK_PurchaseOrderDocuments_Assets] FOREIGN KEY ([AssetId]) REFERENCES [media].[assets] ([id]);
END
GO

IF OBJECT_ID(N'projects.ProjectDocuments', N'U') IS NOT NULL
   AND COL_LENGTH(N'projects.ProjectDocuments', N'AssetId') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_ProjectDocuments_Assets')
BEGIN
    ALTER TABLE [projects].[ProjectDocuments]
    ADD CONSTRAINT [FK_ProjectDocuments_Assets] FOREIGN KEY ([AssetId]) REFERENCES [media].[assets] ([id]);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_QuotationDocuments_QuotationId' AND object_id = OBJECT_ID(N'projects.QuotationDocuments'))
BEGIN
    CREATE INDEX [IX_QuotationDocuments_QuotationId]
        ON [projects].[QuotationDocuments] ([QuotationId] ASC, [AssetId] ASC);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_PurchaseOrderDocuments_PurchaseOrderId' AND object_id = OBJECT_ID(N'projects.PurchaseOrderDocuments'))
BEGIN
    CREATE INDEX [IX_PurchaseOrderDocuments_PurchaseOrderId]
        ON [projects].[PurchaseOrderDocuments] ([PurchaseOrderId] ASC, [AssetId] ASC);
END
GO

IF OBJECT_ID(N'projects.ProjectDocuments', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ProjectDocuments_ProjectId' AND object_id = OBJECT_ID(N'projects.ProjectDocuments'))
BEGIN
    CREATE INDEX [IX_ProjectDocuments_ProjectId]
        ON [projects].[ProjectDocuments] ([ProjectId] ASC, [AssetId] ASC);
END
GO
