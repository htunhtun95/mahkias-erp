-- Read-only gate. Run with sqlcmd -b. No rows are inserted, updated, or deleted.
-- Fails when a document row has no media.assets link.
SET NOCOUNT ON;

DECLARE @QuotationMissing int = 0;
DECLARE @OrderMissing int = 0;
DECLARE @ProjectMissing int = 0;

IF OBJECT_ID(N'media.assets', N'U') IS NULL
BEGIN
    THROW 50033, N'media.assets does not exist. Run Scripts/AddMediaAssets.sql first.', 1;
END

IF COL_LENGTH(N'projects.QuotationDocuments', N'AssetId') IS NULL
   OR COL_LENGTH(N'projects.PurchaseOrderDocuments', N'AssetId') IS NULL
BEGIN
    THROW 50034, N'Document AssetId columns are missing. Run Scripts/AddMediaAssets.sql first.', 1;
END

SELECT @QuotationMissing = COUNT(*)
FROM [projects].[QuotationDocuments]
WHERE AssetId IS NULL
   OR NOT EXISTS (SELECT 1 FROM [media].[assets] AS A WHERE A.id = AssetId);

SELECT @OrderMissing = COUNT(*)
FROM [projects].[PurchaseOrderDocuments]
WHERE AssetId IS NULL
   OR NOT EXISTS (SELECT 1 FROM [media].[assets] AS A WHERE A.id = AssetId);

IF COL_LENGTH(N'projects.ProjectDocuments', N'AssetId') IS NOT NULL
BEGIN
    SELECT @ProjectMissing = COUNT(*)
    FROM [projects].[ProjectDocuments]
    WHERE AssetId IS NULL
       OR NOT EXISTS (SELECT 1 FROM [media].[assets] AS A WHERE A.id = AssetId);
END

SELECT
    QuotationDocuments = (SELECT COUNT(*) FROM [projects].[QuotationDocuments]),
    QuotationUnlinked = @QuotationMissing,
    PurchaseOrderDocuments = (SELECT COUNT(*) FROM [projects].[PurchaseOrderDocuments]),
    PurchaseOrderUnlinked = @OrderMissing,
    ProjectDocuments = CASE
        WHEN OBJECT_ID(N'projects.ProjectDocuments', N'U') IS NULL THEN 0
        ELSE (SELECT COUNT(*) FROM [projects].[ProjectDocuments])
    END,
    ProjectUnlinked = @ProjectMissing,
    Assets = (SELECT COUNT(*) FROM [media].[assets]),
    LegacyQuotationColumns = CASE WHEN COL_LENGTH(N'projects.QuotationDocuments', N'FileName') IS NULL THEN 0 ELSE 1 END,
    LegacyPurchaseOrderColumns = CASE WHEN COL_LENGTH(N'projects.PurchaseOrderDocuments', N'FileName') IS NULL THEN 0 ELSE 1 END,
    LegacyProjectColumns = CASE WHEN COL_LENGTH(N'projects.ProjectDocuments', N'FileName') IS NULL THEN 0 ELSE 1 END;

IF @QuotationMissing > 0 OR @OrderMissing > 0 OR @ProjectMissing > 0
BEGIN
    THROW 50035, N'One or more documents are not linked to media.assets. Do not drop legacy columns.', 1;
END
GO
