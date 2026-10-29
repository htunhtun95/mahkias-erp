CREATE PROCEDURE [dbo].[UpdateQuotationDocument]
(
    @ProjectId INT,
    @QuotationId INT,
    @DocumentId INT,
    @FileName NVARCHAR(260),
    @FileType NVARCHAR(100),
    @GoogleDriveFileId NVARCHAR(128),
    @WebViewLink NVARCHAR(1000)
)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    UPDATE D
    SET
        D.FileName = @FileName,
        D.FileType = @FileType,
        D.GoogleDriveFileId = @GoogleDriveFileId,
        D.WebViewLink = @WebViewLink,
        D.UploadedAt = SYSUTCDATETIME()
    FROM projects.QuotationDocuments AS D
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

    UPDATE projects.Quotations
    SET DriveFileLink = @WebViewLink
    WHERE Id = @QuotationId;

    SELECT
        DocumentId,
        QuotationId,
        FileName,
        FileType,
        GoogleDriveFileId,
        WebViewLink,
        UploadedAt
    FROM projects.QuotationDocuments
    WHERE DocumentId = @DocumentId;
END
