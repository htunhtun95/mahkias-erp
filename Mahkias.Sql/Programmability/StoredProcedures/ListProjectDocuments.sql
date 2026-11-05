CREATE PROCEDURE [dbo].[ListProjectDocuments]
(
    @ProjectId INT
)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        D.DocumentId,
        D.ProjectId,
        A.file_name AS FileName,
        A.file_type AS FileType,
        A.drive_file_id AS GoogleDriveFileId,
        A.file_url AS WebViewLink,
        A.created_at AS UploadedAt,
        A.file_size AS FileSize,
        A.sort_order AS SortOrder,
        D.IsOriginal,
        D.AssetId
    FROM projects.ProjectDocuments AS D
    INNER JOIN media.assets AS A ON A.id = D.AssetId
    WHERE D.ProjectId = @ProjectId
    ORDER BY A.sort_order, A.created_at, D.DocumentId;
END
