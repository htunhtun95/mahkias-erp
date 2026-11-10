-- Restores the legacy file columns from media.assets after ContractMediaAssets.sql.
-- Document rows and asset rows are kept. Drive files are not touched.
-- AssetId, IsOriginal, and media.assets stay in place, so a restored previous API
-- can read the files and the new API can still resolve them.
-- After this script, redeploy the previous API before using the restored columns.
SET NOCOUNT ON;
GO

IF OBJECT_ID(N'media.assets', N'U') IS NULL
BEGIN
    THROW 50036, N'media.assets is missing, so legacy file columns cannot be restored.', 1;
END
GO

IF COL_LENGTH(N'projects.QuotationDocuments', N'FileName') IS NULL
BEGIN
    ALTER TABLE [projects].[QuotationDocuments] ADD
        [FileName] NVARCHAR(260) NULL,
        [FileType] NVARCHAR(100) NULL,
        [GoogleDriveFileId] NVARCHAR(128) NULL,
        [WebViewLink] NVARCHAR(1000) NULL,
        [UploadedAt] DATETIME2 NULL;
END
GO

IF COL_LENGTH(N'projects.PurchaseOrderDocuments', N'FileName') IS NULL
BEGIN
    ALTER TABLE [projects].[PurchaseOrderDocuments] ADD
        [FileName] NVARCHAR(260) NULL,
        [FileType] NVARCHAR(100) NULL,
        [GoogleDriveFileId] NVARCHAR(128) NULL,
        [WebViewLink] NVARCHAR(1000) NULL,
        [UploadedAt] DATETIME2 NULL;
END
GO

IF OBJECT_ID(N'projects.ProjectDocuments', N'U') IS NOT NULL
   AND COL_LENGTH(N'projects.ProjectDocuments', N'FileName') IS NULL
BEGIN
    ALTER TABLE [projects].[ProjectDocuments] ADD
        [FileName] NVARCHAR(260) NULL,
        [FileType] NVARCHAR(100) NULL,
        [GoogleDriveFileId] NVARCHAR(128) NULL,
        [WebViewLink] NVARCHAR(1000) NULL,
        [UploadedAt] DATETIME2 NULL;
END
GO

UPDATE D
SET
    D.FileName = A.file_name,
    D.FileType = A.file_type,
    D.GoogleDriveFileId = ISNULL(A.drive_file_id, N''),
    D.WebViewLink = A.file_url,
    D.UploadedAt = A.created_at
FROM [projects].[QuotationDocuments] AS D
INNER JOIN [media].[assets] AS A ON A.id = D.AssetId
WHERE D.FileName IS NULL;
GO

UPDATE D
SET
    D.FileName = A.file_name,
    D.FileType = A.file_type,
    D.GoogleDriveFileId = ISNULL(A.drive_file_id, N''),
    D.WebViewLink = A.file_url,
    D.UploadedAt = A.created_at
FROM [projects].[PurchaseOrderDocuments] AS D
INNER JOIN [media].[assets] AS A ON A.id = D.AssetId
WHERE D.FileName IS NULL;
GO

IF OBJECT_ID(N'projects.ProjectDocuments', N'U') IS NOT NULL
   AND COL_LENGTH(N'projects.ProjectDocuments', N'FileName') IS NOT NULL
BEGIN
    UPDATE D
    SET
        D.FileName = A.file_name,
        D.FileType = A.file_type,
        D.GoogleDriveFileId = ISNULL(A.drive_file_id, N''),
        D.WebViewLink = A.file_url,
        D.UploadedAt = A.created_at
    FROM [projects].[ProjectDocuments] AS D
    INNER JOIN [media].[assets] AS A ON A.id = D.AssetId
    WHERE D.FileName IS NULL;
END
GO

IF EXISTS (SELECT 1 FROM [projects].[QuotationDocuments] WHERE FileName IS NULL OR AssetId IS NULL)
BEGIN
    THROW 50037, N'Quotation legacy columns could not be restored for every row. No columns were tightened.', 1;
END
GO

IF EXISTS (SELECT 1 FROM [projects].[PurchaseOrderDocuments] WHERE FileName IS NULL OR AssetId IS NULL)
BEGIN
    THROW 50038, N'Purchase order legacy columns could not be restored for every row. No columns were tightened.', 1;
END
GO

IF OBJECT_ID(N'projects.ProjectDocuments', N'U') IS NOT NULL
   AND COL_LENGTH(N'projects.ProjectDocuments', N'FileName') IS NOT NULL
   AND EXISTS (SELECT 1 FROM [projects].[ProjectDocuments] WHERE FileName IS NULL OR AssetId IS NULL)
BEGIN
    THROW 50039, N'Project legacy columns could not be restored for every row. No columns were tightened.', 1;
END
GO

ALTER TABLE [projects].[QuotationDocuments] ALTER COLUMN [FileName] NVARCHAR(260) NOT NULL;
ALTER TABLE [projects].[QuotationDocuments] ALTER COLUMN [FileType] NVARCHAR(100) NOT NULL;
ALTER TABLE [projects].[QuotationDocuments] ALTER COLUMN [GoogleDriveFileId] NVARCHAR(128) NOT NULL;
ALTER TABLE [projects].[QuotationDocuments] ALTER COLUMN [WebViewLink] NVARCHAR(1000) NOT NULL;
ALTER TABLE [projects].[QuotationDocuments] ALTER COLUMN [UploadedAt] DATETIME2 NOT NULL;
GO

ALTER TABLE [projects].[PurchaseOrderDocuments] ALTER COLUMN [FileName] NVARCHAR(260) NOT NULL;
ALTER TABLE [projects].[PurchaseOrderDocuments] ALTER COLUMN [FileType] NVARCHAR(100) NOT NULL;
ALTER TABLE [projects].[PurchaseOrderDocuments] ALTER COLUMN [GoogleDriveFileId] NVARCHAR(128) NOT NULL;
ALTER TABLE [projects].[PurchaseOrderDocuments] ALTER COLUMN [WebViewLink] NVARCHAR(1000) NOT NULL;
ALTER TABLE [projects].[PurchaseOrderDocuments] ALTER COLUMN [UploadedAt] DATETIME2 NOT NULL;
GO

IF OBJECT_ID(N'projects.ProjectDocuments', N'U') IS NOT NULL
   AND COL_LENGTH(N'projects.ProjectDocuments', N'FileName') IS NOT NULL
BEGIN
    ALTER TABLE [projects].[ProjectDocuments] ALTER COLUMN [FileName] NVARCHAR(260) NOT NULL;
    ALTER TABLE [projects].[ProjectDocuments] ALTER COLUMN [FileType] NVARCHAR(100) NOT NULL;
    ALTER TABLE [projects].[ProjectDocuments] ALTER COLUMN [GoogleDriveFileId] NVARCHAR(128) NOT NULL;
    ALTER TABLE [projects].[ProjectDocuments] ALTER COLUMN [WebViewLink] NVARCHAR(1000) NOT NULL;
    ALTER TABLE [projects].[ProjectDocuments] ALTER COLUMN [UploadedAt] DATETIME2 NOT NULL;
END
GO
