CREATE TABLE [projects].[PartDescriptions]
(
    [Id] INT IDENTITY (1, 1) NOT NULL,
    [PartId] INT NOT NULL,
    [Description] NVARCHAR(MAX) NOT NULL,
    [CreatedAt] DATETIME2 CONSTRAINT [DF_PartDescriptions_CreatedAt] DEFAULT (SYSUTCDATETIME()) NOT NULL,
    CONSTRAINT [PK_PartDescriptions] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_PartDescriptions_Parts] FOREIGN KEY ([PartId]) REFERENCES [projects].[Parts] ([Id])
);
GO

CREATE INDEX [IX_PartDescriptions_PartId]
    ON [projects].[PartDescriptions]([PartId] ASC);
