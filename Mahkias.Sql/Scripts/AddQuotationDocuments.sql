IF COL_LENGTH(N'projects.Projects', N'ProcurementFolderId') IS NULL
BEGIN
    ALTER TABLE [projects].[Projects]
    ADD [ProcurementFolderId] NVARCHAR(128) NULL;
END
GO

IF OBJECT_ID(N'projects.QuotationDocuments', N'U') IS NULL
BEGIN
    CREATE TABLE [projects].[QuotationDocuments]
    (
        [DocumentId] INT IDENTITY (1, 1) NOT NULL,
        [QuotationId] INT NOT NULL,
        [FileName] NVARCHAR(260) NOT NULL,
        [FileType] NVARCHAR(100) NOT NULL,
        [GoogleDriveFileId] NVARCHAR(128) NOT NULL,
        [WebViewLink] NVARCHAR(1000) NOT NULL,
        [UploadedAt] DATETIME2 CONSTRAINT [DF_QuotationDocuments_UploadedAt] DEFAULT (SYSUTCDATETIME()) NOT NULL,
        CONSTRAINT [PK_QuotationDocuments] PRIMARY KEY CLUSTERED ([DocumentId] ASC),
        CONSTRAINT [FK_QuotationDocuments_Quotations] FOREIGN KEY ([QuotationId]) REFERENCES [projects].[Quotations] ([Id])
    );

    CREATE INDEX [IX_QuotationDocuments_QuotationId]
        ON [projects].[QuotationDocuments] ([QuotationId] ASC, [UploadedAt] DESC);
END
GO
