CREATE TABLE [projects].[ProjectDocuments]
(
    [DocumentId] INT IDENTITY (1, 1) NOT NULL,
    [ProjectId] INT NOT NULL,
    [AssetId] UNIQUEIDENTIFIER NOT NULL,
    [IsOriginal] BIT CONSTRAINT [DF_ProjectDocuments_IsOriginal] DEFAULT (0) NOT NULL,
    CONSTRAINT [PK_ProjectDocuments] PRIMARY KEY CLUSTERED ([DocumentId] ASC),
    CONSTRAINT [FK_ProjectDocuments_Projects] FOREIGN KEY ([ProjectId]) REFERENCES [projects].[Projects] ([Id]),
    CONSTRAINT [FK_ProjectDocuments_Assets] FOREIGN KEY ([AssetId]) REFERENCES [media].[assets] ([id])
);
GO

CREATE INDEX [IX_ProjectDocuments_ProjectId]
    ON [projects].[ProjectDocuments] ([ProjectId] ASC, [AssetId] ASC);
