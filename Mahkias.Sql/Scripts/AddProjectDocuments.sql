IF COL_LENGTH(N'projects.Projects', N'GoogleDriveFolderId') IS NULL
BEGIN
    ALTER TABLE [projects].[Projects]
    ADD [GoogleDriveFolderId] NVARCHAR(128) NULL;
END
GO

IF OBJECT_ID(N'projects.ProjectDocuments', N'U') IS NULL
BEGIN
    CREATE TABLE [projects].[ProjectDocuments]
    (
        [DocumentId] INT IDENTITY (1, 1) NOT NULL,
        [ProjectId] INT NOT NULL,
        [FileName] NVARCHAR(260) NOT NULL,
        [FileType] NVARCHAR(100) NOT NULL,
        [GoogleDriveFileId] NVARCHAR(128) NOT NULL,
        [WebViewLink] NVARCHAR(1000) NOT NULL,
        [UploadedAt] DATETIME2 CONSTRAINT [DF_ProjectDocuments_UploadedAt] DEFAULT (SYSUTCDATETIME()) NOT NULL,
        CONSTRAINT [PK_ProjectDocuments] PRIMARY KEY CLUSTERED ([DocumentId] ASC),
        CONSTRAINT [FK_ProjectDocuments_Projects] FOREIGN KEY ([ProjectId]) REFERENCES [projects].[Projects] ([Id])
    );

    CREATE INDEX [IX_ProjectDocuments_ProjectId]
        ON [projects].[ProjectDocuments] ([ProjectId] ASC, [UploadedAt] DESC);
END
GO
