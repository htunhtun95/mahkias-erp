CREATE OR ALTER PROCEDURE [dbo].[CreateQuotationDocument]
(
    @ProjectId INT,
    @QuotationId INT,
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
        FROM projects.Quotations AS Q
        INNER JOIN projects.QuotationProjects AS QP ON QP.QuotationId = Q.Id
        WHERE Q.Id = @QuotationId
          AND QP.ProjectId = @ProjectId
          AND Q.IsDeleted = 0
    )
    BEGIN
        ;THROW 50011, N'Quotation was not found on this project.', 1;
    END

    INSERT INTO projects.QuotationDocuments (QuotationId, FileName, FileType, GoogleDriveFileId, WebViewLink)
    VALUES (
        @QuotationId,
        @FileName,
        @FileType,
        @GoogleDriveFileId,
        @WebViewLink
    );

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
    WHERE DocumentId = CAST(SCOPE_IDENTITY() AS INT);
END
