CREATE PROCEDURE [dbo].[UpdateQuotationDocument]
(
    @ProjectId INT,
    @QuotationId INT,
    @DocumentId INT,
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

    UPDATE A
    SET
        A.file_name = @FileName,
        A.file_type = @FileType,
        A.drive_file_id = @GoogleDriveFileId,
        A.file_url = ISNULL(@WebViewLink, N''),
        A.file_size = ISNULL(@FileSize, A.file_size),
        A.sort_order = ISNULL(@SortOrder, A.sort_order)
    FROM media.assets AS A
    INNER JOIN projects.QuotationDocuments AS D ON D.AssetId = A.id
    INNER JOIN projects.Quotations AS Q ON Q.Id = D.QuotationId
    INNER JOIN projects.QuotationProjects AS QP ON QP.QuotationId = Q.Id
    WHERE D.DocumentId = @DocumentId
      AND D.QuotationId = @QuotationId
      AND QP.ProjectId = @ProjectId
      AND Q.IsDeleted = 0;

    IF @@ROWCOUNT = 0
    BEGIN
        ;THROW 50012, N'Quotation file was not found on this project.', 1;
    END

    IF @IsOriginal = 1
    BEGIN
        UPDATE projects.QuotationDocuments
        SET IsOriginal = CASE WHEN DocumentId = @DocumentId THEN 1 ELSE 0 END
        WHERE QuotationId = @QuotationId;
    END

    UPDATE projects.Quotations
    SET DriveFileLink = ISNULL(@WebViewLink, N'')
    WHERE Id = @QuotationId
      AND @IsOriginal = 1;

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
    WHERE D.DocumentId = @DocumentId;
END
