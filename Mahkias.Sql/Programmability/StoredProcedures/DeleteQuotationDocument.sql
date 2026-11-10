CREATE PROCEDURE [dbo].[DeleteQuotationDocument]
(
    @ProjectId INT,
    @QuotationId INT,
    @DocumentId INT
)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @AssetId UNIQUEIDENTIFIER;
    DECLARE @Deleted INT = 0;

    SELECT @AssetId = D.AssetId
    FROM projects.QuotationDocuments AS D
    INNER JOIN projects.Quotations AS Q ON Q.Id = D.QuotationId
    INNER JOIN projects.QuotationProjects AS QP ON QP.QuotationId = Q.Id
    WHERE D.DocumentId = @DocumentId
      AND D.QuotationId = @QuotationId
      AND QP.ProjectId = @ProjectId
      AND Q.IsDeleted = 0;

    DELETE D
    FROM projects.QuotationDocuments AS D
    WHERE D.DocumentId = @DocumentId
      AND D.AssetId = @AssetId;

    SET @Deleted = @@ROWCOUNT;

    IF @Deleted > 0 AND @AssetId IS NOT NULL
       AND NOT EXISTS (SELECT 1 FROM projects.QuotationDocuments WHERE AssetId = @AssetId)
       AND NOT EXISTS (SELECT 1 FROM projects.PurchaseOrderDocuments WHERE AssetId = @AssetId)
       AND NOT EXISTS (SELECT 1 FROM projects.ProjectDocuments WHERE AssetId = @AssetId)
    BEGIN
        DELETE FROM media.assets WHERE id = @AssetId;
    END

    IF @Deleted > 0
    BEGIN
        UPDATE Q
        SET Q.DriveFileLink = Latest.file_url
        FROM projects.Quotations AS Q
        OUTER APPLY (
            SELECT TOP (1) A.file_url
            FROM projects.QuotationDocuments AS D
            INNER JOIN media.assets AS A ON A.id = D.AssetId
            WHERE D.QuotationId = Q.Id
            ORDER BY D.IsOriginal DESC, A.sort_order, A.created_at DESC
        ) AS Latest
        WHERE Q.Id = @QuotationId;
    END

    SELECT Deleted = @Deleted;
END
