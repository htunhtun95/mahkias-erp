SET NOCOUNT ON;

IF OBJECT_ID(N'projects.ActivityGroups', N'U') IS NULL
BEGIN
    CREATE TABLE [projects].[ActivityGroups]
    (
        [Id] INT IDENTITY (1, 1) NOT NULL,
        [ProjectId] INT NOT NULL,
        [Name] NVARCHAR(200) NOT NULL,
        [Description] NVARCHAR(MAX) NULL,
        [CreatedAt] DATETIME CONSTRAINT [DF_ActivityGroups_CreatedAt] DEFAULT (GETDATE()) NOT NULL,
        CONSTRAINT [PK_ActivityGroups] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_ActivityGroups_Projects] FOREIGN KEY ([ProjectId]) REFERENCES [projects].[Projects] ([Id])
    );
END
GO

IF COL_LENGTH(N'projects.Activities', N'ActivityGroupId') IS NULL
BEGIN
    ALTER TABLE [projects].[Activities] ADD [ActivityGroupId] INT NULL;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Activities_ActivityGroups')
BEGIN
    ALTER TABLE [projects].[Activities]
    ADD CONSTRAINT [FK_Activities_ActivityGroups]
        FOREIGN KEY ([ActivityGroupId]) REFERENCES [projects].[ActivityGroups] ([Id]);
END
GO

IF COL_LENGTH(N'projects.QuotationItems', N'ActivityGroupId') IS NULL
BEGIN
    ALTER TABLE [projects].[QuotationItems] ADD [ActivityGroupId] INT NULL;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_QuotationItems_ActivityGroups')
BEGIN
    ALTER TABLE [projects].[QuotationItems]
    ADD CONSTRAINT [FK_QuotationItems_ActivityGroups]
        FOREIGN KEY ([ActivityGroupId]) REFERENCES [projects].[ActivityGroups] ([Id]);
END
GO

ALTER TABLE [projects].[QuotationItems] ALTER COLUMN [UnitPrice] DECIMAL(18, 4) NULL;
ALTER TABLE [projects].[QuotationItems] ALTER COLUMN [TotalPrice] DECIMAL(18, 4) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_Quotations_Code' AND object_id = OBJECT_ID(N'projects.Quotations'))
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX [UX_Quotations_Code]
        ON [projects].[Quotations] ([Code] ASC);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Activities_ActivityGroupId' AND object_id = OBJECT_ID(N'projects.Activities'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_Activities_ActivityGroupId]
        ON [projects].[Activities] ([ActivityGroupId] ASC)
        WHERE [ActivityGroupId] IS NOT NULL;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_QuotationItems_ActivityId_ActivityGroupId' AND object_id = OBJECT_ID(N'projects.QuotationItems'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_QuotationItems_ActivityId_ActivityGroupId]
        ON [projects].[QuotationItems] ([ActivityId] ASC, [ActivityGroupId] ASC);
END
GO
