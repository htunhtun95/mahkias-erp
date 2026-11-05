CREATE PROCEDURE [dbo].[CreateProjectDocument]
(
    @ProjectId INT,
    @FileName NVARCHAR(260),
    @FileType NVARCHAR(100),
    @GoogleDriveFileId NVARCHAR(128),
    @WebViewLink NVARCHAR(1000),
    @FileSize BIGINT = 0,
    @IsOriginal BIT = 0,
    @SortOrder INT = NULL
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

    DECLARE @AssetId UNIQUEIDENTIFIER = NEWID();
    DECLARE @Original BIT = @IsOriginal;
    IF @Original = 0 AND NOT EXISTS (
        SELECT 1 FROM projects.ProjectDocuments WHERE ProjectId = @ProjectId AND IsOriginal = 1
    )
    BEGIN
        SET @Original = 1;
    END

    DECLARE @Sort INT = @SortOrder;
    IF @Sort IS NULL
    BEGIN
        SELECT @Sort = ISNULL(MAX(A.sort_order), 0) + 1
        FROM projects.ProjectDocuments AS D
        INNER JOIN media.assets AS A ON A.id = D.AssetId
        WHERE D.ProjectId = @ProjectId;
    END

    IF @Original = 1
    BEGIN
        UPDATE projects.ProjectDocuments
        SET IsOriginal = 0
        WHERE ProjectId = @ProjectId;
    END

    INSERT INTO media.assets (id, file_url, file_name, file_type, file_size, sort_order, drive_file_id)
    VALUES (@AssetId, ISNULL(@WebViewLink, N''), @FileName, @FileType, ISNULL(@FileSize, 0), ISNULL(@Sort, 0), @GoogleDriveFileId);

    INSERT INTO projects.ProjectDocuments (ProjectId, AssetId, IsOriginal)
    VALUES (@ProjectId, @AssetId, @Original);

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
    WHERE D.DocumentId = CAST(SCOPE_IDENTITY() AS INT);
END
