-- Expand only. Adds DocumentsFolderId and nullable project-document asset columns,
-- then copies legacy file columns into media.assets. Does not drop those columns.
-- Requires Scripts/AddMediaAssets.sql first. ContractMediaAssets.sql drops the
-- legacy columns after verification.
IF COL_LENGTH(N'projects.Projects', N'DocumentsFolderId') IS NULL
BEGIN
    ALTER TABLE [projects].[Projects] ADD [DocumentsFolderId] NVARCHAR(128) NULL;
END
GO

IF COL_LENGTH(N'projects.ProjectDocuments', N'AssetId') IS NULL
BEGIN
    ALTER TABLE [projects].[ProjectDocuments] ADD [AssetId] UNIQUEIDENTIFIER NULL;
END
GO

IF COL_LENGTH(N'projects.ProjectDocuments', N'IsOriginal') IS NULL
BEGIN
    ALTER TABLE [projects].[ProjectDocuments]
    ADD [IsOriginal] BIT CONSTRAINT [DF_ProjectDocuments_IsOriginal] DEFAULT (0) NOT NULL;
END
GO

IF COL_LENGTH(N'projects.ProjectDocuments', N'FileName') IS NOT NULL
BEGIN
    EXEC sys.sp_executesql N'
        DECLARE @ProjectMap TABLE (DocumentId INT NOT NULL, AssetId UNIQUEIDENTIFIER NOT NULL);

        MERGE [media].[assets] AS target
        USING (
            SELECT
                DocumentId,
                FileName,
                FileType,
                ISNULL(WebViewLink, N'''') AS WebViewLink,
                GoogleDriveFileId,
                UploadedAt,
                ROW_NUMBER() OVER (PARTITION BY ProjectId ORDER BY UploadedAt, DocumentId) AS SortOrder
            FROM [projects].[ProjectDocuments]
            WHERE AssetId IS NULL
        ) AS source
        ON 1 = 0
        WHEN NOT MATCHED THEN
            INSERT (id, file_url, file_name, file_type, file_size, sort_order, created_at, drive_file_id)
            VALUES (NEWID(), source.WebViewLink, source.FileName, source.FileType, 0, source.SortOrder, source.UploadedAt, source.GoogleDriveFileId)
        OUTPUT source.DocumentId, inserted.id INTO @ProjectMap (DocumentId, AssetId);

        UPDATE D
        SET D.AssetId = Map.AssetId
        FROM [projects].[ProjectDocuments] AS D
        INNER JOIN @ProjectMap AS Map ON Map.DocumentId = D.DocumentId;

        ;WITH ranked AS (
            SELECT
                D.DocumentId,
                D.ProjectId,
                ROW_NUMBER() OVER (PARTITION BY D.ProjectId ORDER BY D.UploadedAt DESC, D.DocumentId DESC) AS rn
            FROM [projects].[ProjectDocuments] AS D
            INNER JOIN @ProjectMap AS Map ON Map.DocumentId = D.DocumentId
        )
        UPDATE D
        SET D.IsOriginal = CASE
            WHEN EXISTS (
                SELECT 1
                FROM [projects].[ProjectDocuments] AS Existing
                WHERE Existing.ProjectId = ranked.ProjectId
                  AND Existing.IsOriginal = 1
                  AND Existing.DocumentId NOT IN (SELECT DocumentId FROM @ProjectMap)
            ) THEN 0
            WHEN ranked.rn = 1 THEN 1
            ELSE 0
        END
        FROM [projects].[ProjectDocuments] AS D
        INNER JOIN ranked ON ranked.DocumentId = D.DocumentId;
    ';
END
GO

IF COL_LENGTH(N'projects.ProjectDocuments', N'FileName') IS NOT NULL
   AND EXISTS (SELECT 1 FROM [projects].[ProjectDocuments] WHERE AssetId IS NULL)
BEGIN
    THROW 50032, N'Project documents could not be linked to media assets. Legacy columns were left in place.', 1;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_ProjectDocuments_Assets')
BEGIN
    ALTER TABLE [projects].[ProjectDocuments]
    ADD CONSTRAINT [FK_ProjectDocuments_Assets] FOREIGN KEY ([AssetId]) REFERENCES [media].[assets] ([id]);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ProjectDocuments_ProjectId' AND object_id = OBJECT_ID(N'projects.ProjectDocuments'))
BEGIN
    CREATE INDEX [IX_ProjectDocuments_ProjectId]
        ON [projects].[ProjectDocuments] ([ProjectId] ASC, [AssetId] ASC);
END
GO
