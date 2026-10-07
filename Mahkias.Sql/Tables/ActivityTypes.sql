CREATE TABLE [projects].[ActivityTypes]
(
    [Id] INT NOT NULL,
    [Name] NVARCHAR(64) NULL,
    [Slug] NVARCHAR(64) NULL,
    CONSTRAINT [PK_ActivityTypes] PRIMARY KEY CLUSTERED ([Id] ASC)
);
GO

CREATE UNIQUE INDEX [IX_ActivityTypes_Slug]
    ON [projects].[ActivityTypes]([Slug] ASC);
