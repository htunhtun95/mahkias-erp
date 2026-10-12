CREATE OR ALTER PROCEDURE [dbo].[ListProjectDocuments]
(
    @ProjectId INT
)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        DocumentId,
        ProjectId,
        FileName,
        FileType,
        GoogleDriveFileId,
        WebViewLink,
        UploadedAt
    FROM projects.ProjectDocuments
    WHERE ProjectId = @ProjectId
    ORDER BY UploadedAt DESC, DocumentId DESC;
END
GO
