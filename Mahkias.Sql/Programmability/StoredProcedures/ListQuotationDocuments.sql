CREATE PROCEDURE [dbo].[ListQuotationDocuments]
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
        A.file_name AS FileName,
        A.file_type AS FileType,
        A.drive_file_id AS GoogleDriveFileId,
        A.file_url AS WebViewLink,
        A.created_at AS UploadedAt,
        A.file_size AS FileSize,
        A.sort_order AS SortOrder,
        D.IsOriginal,
        D.AssetId
    FROM projects.QuotationDocuments AS D
    INNER JOIN media.assets AS A ON A.id = D.AssetId
    INNER JOIN projects.Quotations AS Q ON Q.Id = D.QuotationId
    INNER JOIN projects.QuotationProjects AS QP ON QP.QuotationId = Q.Id
    WHERE D.QuotationId = @QuotationId
      AND QP.ProjectId = @ProjectId
      AND Q.IsDeleted = 0
    ORDER BY A.sort_order, A.created_at, D.DocumentId;
END
