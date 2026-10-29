CREATE PROCEDURE [dbo].[CreateProjectDocument]
(
    @ProjectId INT,
    @FileName NVARCHAR(260),
    @FileType NVARCHAR(100),
    @GoogleDriveFileId NVARCHAR(128),
    @WebViewLink NVARCHAR(1000)
)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF NOT EXISTS (
        SELECT 1
        FROM projects.Projects
        WHERE Id = @ProjectId
          AND IsDeleted = 0
    )
    BEGIN
        ;THROW 50010, N'Project was not found.', 1;
    END

    INSERT INTO projects.ProjectDocuments (ProjectId, FileName, FileType, GoogleDriveFileId, WebViewLink)
    VALUES (
        @ProjectId,
        @FileName,
        @FileType,
        @GoogleDriveFileId,
        @WebViewLink
    );

    SELECT
        DocumentId,
        ProjectId,
        FileName,
        FileType,
        GoogleDriveFileId,
        WebViewLink,
        UploadedAt
    FROM projects.ProjectDocuments
    WHERE DocumentId = CAST(SCOPE_IDENTITY() AS INT);
END
