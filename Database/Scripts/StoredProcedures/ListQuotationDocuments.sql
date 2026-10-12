CREATE OR ALTER PROCEDURE [dbo].[ListQuotationDocuments]
(
    @ProjectId INT,
    @QuotationId INT
)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        D.DocumentId,
        D.QuotationId,
        D.FileName,
        D.FileType,
        D.GoogleDriveFileId,
        D.WebViewLink,
        D.UploadedAt
    FROM projects.QuotationDocuments AS D
    INNER JOIN projects.Quotations AS Q ON Q.Id = D.QuotationId
    INNER JOIN projects.QuotationProjects AS QP ON QP.QuotationId = Q.Id
    WHERE D.QuotationId = @QuotationId
      AND QP.ProjectId = @ProjectId
      AND Q.IsDeleted = 0
    ORDER BY D.UploadedAt DESC, D.DocumentId DESC;
END
GO
