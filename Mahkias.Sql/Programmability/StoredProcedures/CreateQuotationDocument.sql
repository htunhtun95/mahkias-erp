CREATE PROCEDURE [dbo].[CreateQuotationDocument]
(
    @ProjectId INT,
    @QuotationId INT,
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
        FROM projects.Quotations AS Q
        INNER JOIN projects.QuotationProjects AS QP ON QP.QuotationId = Q.Id
        WHERE Q.Id = @QuotationId
          AND QP.ProjectId = @ProjectId
          AND Q.IsDeleted = 0
    )
    BEGIN
        ;THROW 50011, N'Quotation was not found on this project.', 1;
    END

    DECLARE @AssetId UNIQUEIDENTIFIER = NEWID();
    DECLARE @Original BIT = @IsOriginal;
    IF @Original = 0 AND NOT EXISTS (
        SELECT 1 FROM projects.QuotationDocuments WHERE QuotationId = @QuotationId AND IsOriginal = 1
    )
    BEGIN
        SET @Original = 1;
    END

    DECLARE @Sort INT = @SortOrder;
    IF @Sort IS NULL
    BEGIN
        SELECT @Sort = ISNULL(MAX(A.sort_order), 0) + 1
        FROM projects.QuotationDocuments AS D
        INNER JOIN media.assets AS A ON A.id = D.AssetId
        WHERE D.QuotationId = @QuotationId;
    END

    IF @Original = 1
    BEGIN
        UPDATE projects.QuotationDocuments
        SET IsOriginal = 0
        WHERE QuotationId = @QuotationId;
    END

    INSERT INTO media.assets (id, file_url, file_name, file_type, file_size, sort_order, drive_file_id)
    VALUES (@AssetId, ISNULL(@WebViewLink, N''), @FileName, @FileType, ISNULL(@FileSize, 0), ISNULL(@Sort, 0), @GoogleDriveFileId);

    INSERT INTO projects.QuotationDocuments (QuotationId, AssetId, IsOriginal)
    VALUES (@QuotationId, @AssetId, @Original);

    UPDATE projects.Quotations
    SET DriveFileLink = ISNULL(@WebViewLink, N'')
    WHERE Id = @QuotationId
      AND @Original = 1;

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
    WHERE D.DocumentId = CAST(SCOPE_IDENTITY() AS INT);
END
