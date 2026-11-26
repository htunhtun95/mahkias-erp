SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

IF OBJECT_ID(N'projects.PartAlternates', N'U') IS NULL
BEGIN
    CREATE TABLE [projects].[PartAlternates]
    (
        [Id] INT IDENTITY (1, 1) NOT NULL,
        [PartId] INT NOT NULL,
        [AlternatePartId] INT NOT NULL,
        [CreatedAt] DATETIME2 CONSTRAINT [DF_PartAlternates_CreatedAt] DEFAULT (SYSUTCDATETIME()) NOT NULL,
        CONSTRAINT [PK_PartAlternates] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_PartAlternates_Parts] FOREIGN KEY ([PartId]) REFERENCES [projects].[Parts] ([Id]),
        CONSTRAINT [FK_PartAlternates_AlternateParts] FOREIGN KEY ([AlternatePartId]) REFERENCES [projects].[Parts] ([Id]),
        CONSTRAINT [CK_PartAlternates_NotSelf] CHECK ([PartId] <> [AlternatePartId])
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_PartAlternates_Part_Alternate' AND object_id = OBJECT_ID(N'projects.PartAlternates'))
BEGIN
    CREATE UNIQUE INDEX [UX_PartAlternates_Part_Alternate]
        ON [projects].[PartAlternates]([PartId] ASC, [AlternatePartId] ASC);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_PartAlternates_AlternatePartId' AND object_id = OBJECT_ID(N'projects.PartAlternates'))
BEGIN
    CREATE INDEX [IX_PartAlternates_AlternatePartId]
        ON [projects].[PartAlternates]([AlternatePartId] ASC);
END
GO
