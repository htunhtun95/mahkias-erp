-- Expand only. Adds media.assets and nullable AssetId / IsOriginal, then copies legacy
-- file columns into media.assets. Does not drop the legacy columns. Run
-- Scripts/ContractMediaAssets.sql only after the new API is live and
-- Scripts/VerifyMediaAssets.sql reports no unlinked documents.
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'media')
BEGIN
    EXEC(N'CREATE SCHEMA [media] AUTHORIZATION [dbo]');
END
GO

IF OBJECT_ID(N'media.assets', N'U') IS NULL
BEGIN
    CREATE TABLE [media].[assets]
    (
        [id] UNIQUEIDENTIFIER CONSTRAINT [DF_media_assets_id] DEFAULT (NEWSEQUENTIALID()) NOT NULL,
        [file_url] NVARCHAR(1000) NOT NULL,
        [file_name] NVARCHAR(260) NOT NULL,
        [file_type] NVARCHAR(100) NOT NULL,
        [file_size] BIGINT CONSTRAINT [DF_media_assets_file_size] DEFAULT (0) NOT NULL,
        [sort_order] INT CONSTRAINT [DF_media_assets_sort_order] DEFAULT (0) NOT NULL,
        [created_at] DATETIME2 CONSTRAINT [DF_media_assets_created_at] DEFAULT (SYSUTCDATETIME()) NOT NULL,
        [drive_file_id] NVARCHAR(128) NULL,
        CONSTRAINT [PK_media_assets] PRIMARY KEY CLUSTERED ([id] ASC)
    );
END
GO

IF COL_LENGTH(N'projects.QuotationDocuments', N'AssetId') IS NULL
BEGIN
    ALTER TABLE [projects].[QuotationDocuments] ADD [AssetId] UNIQUEIDENTIFIER NULL;
END
GO

IF COL_LENGTH(N'projects.QuotationDocuments', N'IsOriginal') IS NULL
BEGIN
    ALTER TABLE [projects].[QuotationDocuments]
    ADD [IsOriginal] BIT CONSTRAINT [DF_QuotationDocuments_IsOriginal] DEFAULT (0) NOT NULL;
END
GO

IF COL_LENGTH(N'projects.PurchaseOrderDocuments', N'AssetId') IS NULL
BEGIN
    ALTER TABLE [projects].[PurchaseOrderDocuments] ADD [AssetId] UNIQUEIDENTIFIER NULL;
END
GO

IF COL_LENGTH(N'projects.PurchaseOrderDocuments', N'IsOriginal') IS NULL
BEGIN
    ALTER TABLE [projects].[PurchaseOrderDocuments]
    ADD [IsOriginal] BIT CONSTRAINT [DF_PurchaseOrderDocuments_IsOriginal] DEFAULT (0) NOT NULL;
END
GO

IF COL_LENGTH(N'projects.QuotationDocuments', N'FileName') IS NOT NULL
BEGIN
    EXEC sys.sp_executesql N'
        DECLARE @QuotationMap TABLE (DocumentId INT NOT NULL, AssetId UNIQUEIDENTIFIER NOT NULL);

        MERGE [media].[assets] AS target
        USING (
            SELECT
                DocumentId,
                FileName,
                FileType,
                ISNULL(WebViewLink, N'''') AS WebViewLink,
                GoogleDriveFileId,
                UploadedAt,
                ROW_NUMBER() OVER (PARTITION BY QuotationId ORDER BY UploadedAt, DocumentId) AS SortOrder
            FROM [projects].[QuotationDocuments]
            WHERE AssetId IS NULL
        ) AS source
        ON 1 = 0
        WHEN NOT MATCHED THEN
            INSERT (id, file_url, file_name, file_type, file_size, sort_order, created_at, drive_file_id)
            VALUES (NEWID(), source.WebViewLink, source.FileName, source.FileType, 0, source.SortOrder, source.UploadedAt, source.GoogleDriveFileId)
        OUTPUT source.DocumentId, inserted.id INTO @QuotationMap (DocumentId, AssetId);

        UPDATE D
        SET D.AssetId = Map.AssetId
        FROM [projects].[QuotationDocuments] AS D
        INNER JOIN @QuotationMap AS Map ON Map.DocumentId = D.DocumentId;

        ;WITH ranked AS (
            SELECT
                D.DocumentId,
                D.QuotationId,
                ROW_NUMBER() OVER (PARTITION BY D.QuotationId ORDER BY D.UploadedAt DESC, D.DocumentId DESC) AS rn
            FROM [projects].[QuotationDocuments] AS D
            INNER JOIN @QuotationMap AS Map ON Map.DocumentId = D.DocumentId
        )
        UPDATE D
        SET D.IsOriginal = CASE
            WHEN EXISTS (
                SELECT 1
                FROM [projects].[QuotationDocuments] AS Existing
                WHERE Existing.QuotationId = ranked.QuotationId
                  AND Existing.IsOriginal = 1
                  AND Existing.DocumentId NOT IN (SELECT DocumentId FROM @QuotationMap)
            ) THEN 0
            WHEN ranked.rn = 1 THEN 1
            ELSE 0
        END
        FROM [projects].[QuotationDocuments] AS D
        INNER JOIN ranked ON ranked.DocumentId = D.DocumentId;
    ';
END
GO

IF COL_LENGTH(N'projects.PurchaseOrderDocuments', N'FileName') IS NOT NULL
BEGIN
    EXEC sys.sp_executesql N'
        DECLARE @OrderMap TABLE (DocumentId INT NOT NULL, AssetId UNIQUEIDENTIFIER NOT NULL);

        MERGE [media].[assets] AS target
        USING (
            SELECT
                DocumentId,
                FileName,
                FileType,
                ISNULL(WebViewLink, N'''') AS WebViewLink,
                GoogleDriveFileId,
                UploadedAt,
                ROW_NUMBER() OVER (PARTITION BY PurchaseOrderId ORDER BY UploadedAt, DocumentId) AS SortOrder
            FROM [projects].[PurchaseOrderDocuments]
            WHERE AssetId IS NULL
        ) AS source
        ON 1 = 0
        WHEN NOT MATCHED THEN
            INSERT (id, file_url, file_name, file_type, file_size, sort_order, created_at, drive_file_id)
            VALUES (NEWID(), source.WebViewLink, source.FileName, source.FileType, 0, source.SortOrder, source.UploadedAt, source.GoogleDriveFileId)
        OUTPUT source.DocumentId, inserted.id INTO @OrderMap (DocumentId, AssetId);

        UPDATE D
        SET D.AssetId = Map.AssetId
        FROM [projects].[PurchaseOrderDocuments] AS D
        INNER JOIN @OrderMap AS Map ON Map.DocumentId = D.DocumentId;

        ;WITH ranked AS (
            SELECT
                D.DocumentId,
                D.PurchaseOrderId,
                ROW_NUMBER() OVER (PARTITION BY D.PurchaseOrderId ORDER BY D.UploadedAt DESC, D.DocumentId DESC) AS rn
            FROM [projects].[PurchaseOrderDocuments] AS D
            INNER JOIN @OrderMap AS Map ON Map.DocumentId = D.DocumentId
        )
        UPDATE D
        SET D.IsOriginal = CASE
            WHEN EXISTS (
                SELECT 1
                FROM [projects].[PurchaseOrderDocuments] AS Existing
                WHERE Existing.PurchaseOrderId = ranked.PurchaseOrderId
                  AND Existing.IsOriginal = 1
                  AND Existing.DocumentId NOT IN (SELECT DocumentId FROM @OrderMap)
            ) THEN 0
            WHEN ranked.rn = 1 THEN 1
            ELSE 0
        END
        FROM [projects].[PurchaseOrderDocuments] AS D
        INNER JOIN ranked ON ranked.DocumentId = D.DocumentId;
    ';
END
GO

IF COL_LENGTH(N'projects.QuotationDocuments', N'FileName') IS NOT NULL
   AND EXISTS (SELECT 1 FROM [projects].[QuotationDocuments] WHERE AssetId IS NULL)
BEGIN
    THROW 50030, N'Quotation documents could not be linked to media assets. Legacy columns were left in place.', 1;
END
GO

IF COL_LENGTH(N'projects.PurchaseOrderDocuments', N'FileName') IS NOT NULL
   AND EXISTS (SELECT 1 FROM [projects].[PurchaseOrderDocuments] WHERE AssetId IS NULL)
BEGIN
    THROW 50031, N'Purchase order documents could not be linked to media assets. Legacy columns were left in place.', 1;
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
