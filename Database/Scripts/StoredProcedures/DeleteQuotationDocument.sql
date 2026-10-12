CREATE OR ALTER PROCEDURE [dbo].[DeleteQuotationDocument]
(
    @ProjectId INT,
    @QuotationId INT,
    @DocumentId INT
)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @Deleted INT = 0;

    DELETE D
    FROM projects.QuotationDocuments AS D
    INNER JOIN projects.Quotations AS Q ON Q.Id = D.QuotationId
    INNER JOIN projects.QuotationProjects AS QP ON QP.QuotationId = Q.Id
    WHERE D.DocumentId = @DocumentId
      AND D.QuotationId = @QuotationId
      AND QP.ProjectId = @ProjectId
      AND Q.IsDeleted = 0;

    SET @Deleted = @@ROWCOUNT;

    IF @Deleted > 0
    BEGIN
        UPDATE Q
        SET Q.DriveFileLink = Latest.WebViewLink
        FROM projects.Quotations AS Q
        OUTER APPLY (
            SELECT TOP (1) D.WebViewLink
            FROM projects.QuotationDocuments AS D
            WHERE D.QuotationId = Q.Id
            ORDER BY D.UploadedAt DESC, D.DocumentId DESC
        ) AS Latest
        WHERE Q.Id = @QuotationId;
    END

    SELECT Deleted = @Deleted;
END
GO
