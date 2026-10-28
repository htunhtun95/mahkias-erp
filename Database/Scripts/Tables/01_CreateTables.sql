-- Mahkias ERP tables for Azure SQL Database.
-- Safe to re-run: existing tables and indexes are left in place.

IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'projects')
    EXEC(N'CREATE SCHEMA [projects] AUTHORIZATION [dbo];');
GO

IF OBJECT_ID(N'dbo.Currencies', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[Currencies]
    (
        [Id] INT IDENTITY (1, 1) NOT NULL,
        [Code] NVARCHAR(3) NOT NULL,
        [Name] NVARCHAR(100) NOT NULL,
        [Symbol] NVARCHAR(10) NOT NULL,
        [IsDefault] BIT CONSTRAINT [DF_Currencies_IsDefault] DEFAULT (0) NOT NULL,
        [IsActive] BIT CONSTRAINT [DF_Currencies_IsActive] DEFAULT (1) NOT NULL,
        CONSTRAINT [PK_Currencies] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [UX_Currencies_Code] UNIQUE ([Code] ASC)
    );
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'UX_Currencies_SingleDefault' AND object_id = OBJECT_ID(N'dbo.Currencies')
)
    CREATE UNIQUE INDEX [UX_Currencies_SingleDefault]
        ON [dbo].[Currencies] ([IsDefault])
        WHERE [IsDefault] = 1;
GO

IF OBJECT_ID(N'dbo.ExchangeRates', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[ExchangeRates]
    (
        [ExchangeRateId] INT IDENTITY (1, 1) NOT NULL,
        [FromCurrencyId] INT NOT NULL,
        [ToCurrencyId] INT NOT NULL,
        [Rate] DECIMAL(18, 6) NOT NULL,
        [EffectiveDate] DATE NOT NULL,
        CONSTRAINT [PK_ExchangeRates] PRIMARY KEY CLUSTERED ([ExchangeRateId] ASC),
        CONSTRAINT [FK_ExchangeRates_FromCurrency] FOREIGN KEY ([FromCurrencyId]) REFERENCES [dbo].[Currencies] ([Id]),
        CONSTRAINT [FK_ExchangeRates_ToCurrency] FOREIGN KEY ([ToCurrencyId]) REFERENCES [dbo].[Currencies] ([Id]),
        CONSTRAINT [UX_ExchangeRates_PairDate] UNIQUE ([FromCurrencyId], [ToCurrencyId], [EffectiveDate]),
        CONSTRAINT [CK_ExchangeRates_DistinctPair] CHECK ([FromCurrencyId] <> [ToCurrencyId]),
        CONSTRAINT [CK_ExchangeRates_Rate] CHECK ([Rate] > 0)
    );
END
GO

IF OBJECT_ID(N'projects.ActivityTypes', N'U') IS NULL
BEGIN
    CREATE TABLE [projects].[ActivityTypes]
    (
        [Id] INT NOT NULL,
        [Name] NVARCHAR(64) NULL,
        [Slug] NVARCHAR(64) NULL,
        CONSTRAINT [PK_ActivityTypes] PRIMARY KEY CLUSTERED ([Id] ASC)
    );
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_ActivityTypes_Slug' AND object_id = OBJECT_ID(N'projects.ActivityTypes')
)
    CREATE UNIQUE INDEX [IX_ActivityTypes_Slug]
        ON [projects].[ActivityTypes]([Slug] ASC);
GO

IF OBJECT_ID(N'projects.Projects', N'U') IS NULL
BEGIN
    CREATE TABLE [projects].[Projects]
    (
        [Id] INT IDENTITY (1, 1) NOT NULL,
        [Name] NVARCHAR(250) NOT NULL,
        [Reference] NVARCHAR(100) NULL,
        [Description] NVARCHAR(MAX) NULL,
        [CreatedAt] DATETIME CONSTRAINT [DF_Projects_CreatedAt] DEFAULT (GETDATE()) NOT NULL,
        [ModifiedAt] DATETIME NULL,
        [IsDeleted] BIT CONSTRAINT [DF_Projects_IsDeleted] DEFAULT (0) NOT NULL,
        [DeletedAt] DATETIME2 NULL,
        [DeletedBy] NVARCHAR(100) NULL,
        [GoogleDriveFolderId] NVARCHAR(128) NULL,
        [ProcurementFolderId] NVARCHAR(128) NULL,
        CONSTRAINT [PK_Projects] PRIMARY KEY CLUSTERED ([Id] ASC)
    );
END
GO

IF OBJECT_ID(N'projects.ActivityGroups', N'U') IS NULL
BEGIN
    CREATE TABLE [projects].[ActivityGroups]
    (
        [Id] INT IDENTITY (1, 1) NOT NULL,
        [ProjectId] INT NOT NULL,
        [Name] NVARCHAR(200) NOT NULL,
        [Description] NVARCHAR(MAX) NULL,
        [ActivityTypeId] INT NULL,
        [CreatedAt] DATETIME CONSTRAINT [DF_ActivityGroups_CreatedAt] DEFAULT (GETDATE()) NOT NULL,
        CONSTRAINT [PK_ActivityGroups] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_ActivityGroups_Projects] FOREIGN KEY ([ProjectId]) REFERENCES [projects].[Projects] ([Id]),
        CONSTRAINT [FK_ActivityGroups_ActivityTypes] FOREIGN KEY ([ActivityTypeId]) REFERENCES [projects].[ActivityTypes] ([Id])
    );
END
GO

IF OBJECT_ID(N'projects.Activities', N'U') IS NULL
BEGIN
    CREATE TABLE [projects].[Activities]
    (
        [Id] INT IDENTITY (1, 1) NOT NULL,
        [ProjectId] INT NOT NULL,
        [PartNo] NVARCHAR(MAX) NULL,
        [Budget] DECIMAL(18, 2) NULL,
        [Description] NVARCHAR(MAX) NULL,
        [DSNNo] NVARCHAR(100) NULL,
        [Quantity] DECIMAL(18, 2) NULL,
        [ActivityTypeId] INT NULL,
        [ActivityGroupId] INT NULL,
        [CreatedAt] DATETIME CONSTRAINT [DF_Activities_CreatedAt] DEFAULT (GETDATE()) NOT NULL,
        [ModifiedAt] DATETIME NULL,
        [IsDeleted] BIT CONSTRAINT [DF_Activities_IsDeleted] DEFAULT (0) NOT NULL,
        [DeletedAt] DATETIME2 NULL,
        [DeletedBy] NVARCHAR(100) NULL,
        CONSTRAINT [PK_Activities] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_Activities_Projects] FOREIGN KEY ([ProjectId]) REFERENCES [projects].[Projects] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_Activities_ActivityTypes] FOREIGN KEY ([ActivityTypeId]) REFERENCES [projects].[ActivityTypes] ([Id]),
        CONSTRAINT [FK_Activities_ActivityGroups] FOREIGN KEY ([ActivityGroupId]) REFERENCES [projects].[ActivityGroups] ([Id])
    );
END
GO

IF OBJECT_ID(N'projects.ActivityAlternativeParts', N'U') IS NULL
BEGIN
    CREATE TABLE [projects].[ActivityAlternativeParts]
    (
        [Id] INT IDENTITY (1, 1) NOT NULL,
        [ActivityId] INT NOT NULL,
        [ProjectId] INT NOT NULL,
        [MainPartNo] NVARCHAR(100) NOT NULL,
        [AlternativePartNo] NVARCHAR(100) NOT NULL,
        [SortOrder] INT CONSTRAINT [DF_ActivityAlternativeParts_SortOrder] DEFAULT (1) NOT NULL,
        [CreatedAt] DATETIME CONSTRAINT [DF_ActivityAlternativeParts_CreatedAt] DEFAULT (GETDATE()) NOT NULL,
        [ModifiedAt] DATETIME NULL,
        CONSTRAINT [PK_ActivityAlternativeParts] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_ActivityAlternativeParts_Activities] FOREIGN KEY ([ActivityId]) REFERENCES [projects].[Activities] ([Id]) ON DELETE CASCADE
    );
END
GO

IF OBJECT_ID(N'projects.ActivityLabels', N'U') IS NULL
BEGIN
    CREATE TABLE [projects].[ActivityLabels]
    (
        [Id] INT IDENTITY (1, 1) NOT NULL,
        [ProjectId] INT NOT NULL,
        [Name] NVARCHAR(200) NOT NULL,
        [ActivityTypeId] INT NOT NULL,
        [CreatedAt] DATETIME CONSTRAINT [DF_ActivityLabels_CreatedAt] DEFAULT (GETDATE()) NOT NULL,
        CONSTRAINT [PK_ActivityLabels] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_ActivityLabels_Projects] FOREIGN KEY ([ProjectId]) REFERENCES [projects].[Projects] ([Id]),
        CONSTRAINT [FK_ActivityLabels_ActivityTypes] FOREIGN KEY ([ActivityTypeId]) REFERENCES [projects].[ActivityTypes] ([Id]),
        CONSTRAINT [UQ_ActivityLabels_Project_Type_Name] UNIQUE ([ProjectId], [ActivityTypeId], [Name])
    );
END
GO

IF OBJECT_ID(N'projects.ActivityLabelActivities', N'U') IS NULL
BEGIN
    CREATE TABLE [projects].[ActivityLabelActivities]
    (
        [ActivityLabelId] INT NOT NULL,
        [ActivityId] INT NOT NULL,
        CONSTRAINT [PK_ActivityLabelActivities] PRIMARY KEY CLUSTERED ([ActivityLabelId] ASC, [ActivityId] ASC),
        CONSTRAINT [FK_ActivityLabelActivities_ActivityLabels] FOREIGN KEY ([ActivityLabelId]) REFERENCES [projects].[ActivityLabels] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_ActivityLabelActivities_Activities] FOREIGN KEY ([ActivityId]) REFERENCES [projects].[Activities] ([Id]) ON DELETE CASCADE
    );
END
GO

IF OBJECT_ID(N'projects.ActivityLabelGroups', N'U') IS NULL
BEGIN
    CREATE TABLE [projects].[ActivityLabelGroups]
    (
        [ActivityLabelId] INT NOT NULL,
        [ActivityGroupId] INT NOT NULL,
        CONSTRAINT [PK_ActivityLabelGroups] PRIMARY KEY CLUSTERED ([ActivityLabelId] ASC, [ActivityGroupId] ASC),
        CONSTRAINT [FK_ActivityLabelGroups_ActivityLabels] FOREIGN KEY ([ActivityLabelId]) REFERENCES [projects].[ActivityLabels] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_ActivityLabelGroups_ActivityGroups] FOREIGN KEY ([ActivityGroupId]) REFERENCES [projects].[ActivityGroups] ([Id]) ON DELETE CASCADE
    );
END
GO

IF OBJECT_ID(N'projects.Suppliers', N'U') IS NULL
BEGIN
    CREATE TABLE [projects].[Suppliers]
    (
        [Id] INT IDENTITY (1, 1) NOT NULL,
        [Name] NVARCHAR(250) NOT NULL,
        [Reference] NVARCHAR(100) NULL,
        [CreatedAt] DATETIME CONSTRAINT [DF_Suppliers_CreatedAt] DEFAULT (GETDATE()) NOT NULL,
        [ModifiedAt] DATETIME NULL,
        [IsDeleted] BIT CONSTRAINT [DF_Suppliers_IsDeleted] DEFAULT (0) NOT NULL,
        [DeletedAt] DATETIME2 NULL,
        [DeletedBy] NVARCHAR(100) NULL,
        CONSTRAINT [PK_Suppliers] PRIMARY KEY CLUSTERED ([Id] ASC)
    );
END
GO

IF OBJECT_ID(N'projects.Quotations', N'U') IS NULL
BEGIN
    CREATE TABLE [projects].[Quotations]
    (
        [Id] INT IDENTITY (1, 1) NOT NULL,
        [SupplierId] INT NOT NULL,
        [Code] NVARCHAR(50) NOT NULL,
        [SupplierReference] NVARCHAR(100) NULL,
        [DriveFileLink] NVARCHAR(1000) NULL,
        [CurrencyId] INT NOT NULL,
        [ExchangeRate] DECIMAL(18, 6) CONSTRAINT [DF_Quotations_ExchangeRate] DEFAULT (1.0) NOT NULL,
        [BaseTotalAmount] DECIMAL(18, 2) CONSTRAINT [DF_Quotations_BaseTotalAmount] DEFAULT (0) NOT NULL,
        [CreatedAt] DATETIME CONSTRAINT [DF_Quotations_CreatedAt] DEFAULT (GETDATE()) NOT NULL,
        [ModifiedAt] DATETIME NULL,
        [IsDeleted] BIT CONSTRAINT [DF_Quotations_IsDeleted] DEFAULT (0) NOT NULL,
        [DeletedAt] DATETIME2 NULL,
        [DeletedBy] NVARCHAR(100) NULL,
        CONSTRAINT [PK_Quotations] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_Quotations_Suppliers] FOREIGN KEY ([SupplierId]) REFERENCES [projects].[Suppliers] ([Id]),
        CONSTRAINT [FK_Quotations_Currencies] FOREIGN KEY ([CurrencyId]) REFERENCES [dbo].[Currencies] ([Id])
    );
END
GO

IF OBJECT_ID(N'projects.QuotationProjects', N'U') IS NULL
BEGIN
    CREATE TABLE [projects].[QuotationProjects]
    (
        [QuotationId] INT NOT NULL,
        [ProjectId] INT NOT NULL,
        CONSTRAINT [PK_QuotationProjects] PRIMARY KEY CLUSTERED ([QuotationId] ASC, [ProjectId] ASC),
        CONSTRAINT [FK_QuotationProjects_Quotations] FOREIGN KEY ([QuotationId]) REFERENCES [projects].[Quotations] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_QuotationProjects_Projects] FOREIGN KEY ([ProjectId]) REFERENCES [projects].[Projects] ([Id]) ON DELETE CASCADE
    );
END
GO

IF OBJECT_ID(N'projects.QuotationItems', N'U') IS NULL
BEGIN
    CREATE TABLE [projects].[QuotationItems]
    (
        [Id] INT IDENTITY (1, 1) NOT NULL,
        [QuotationId] INT NOT NULL,
        [ActivityId] INT NULL,
        [ActivityGroupId] INT NULL,
        [IsAlternativePart] BIT CONSTRAINT [DF_QuotationItems_IsAlternativePart] DEFAULT (0) NOT NULL,
        [AlternativePartId] INT NULL,
        [DsnNo] NVARCHAR(100) NULL,
        [PartNo] NVARCHAR(100) NULL,
        [PartDescription] NVARCHAR(500) NULL,
        [CurrencyId] INT NOT NULL,
        [ExchangeRate] DECIMAL(18, 6) CONSTRAINT [DF_QuotationItems_ExchangeRate] DEFAULT (1.0) NOT NULL,
        [UnitPrice] DECIMAL(18, 2) NULL,
        [BaseUnitPrice] DECIMAL(18, 2) NULL,
        [Quantity] INT CONSTRAINT [DF_QuotationItems_Quantity] DEFAULT (1) NOT NULL,
        [TotalPrice] DECIMAL(18, 2) NULL,
        [BaseTotalPrice] DECIMAL(18, 2) NULL,
        [AwardedQuantity] INT CONSTRAINT [DF_QuotationItems_AwardedQuantity] DEFAULT (0) NOT NULL,
        [CreatedAt] DATETIME CONSTRAINT [DF_QuotationItems_CreatedAt] DEFAULT (GETDATE()) NOT NULL,
        [ModifiedAt] DATETIME NULL,
        CONSTRAINT [PK_QuotationItems] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_QuotationItems_Quotations] FOREIGN KEY ([QuotationId]) REFERENCES [projects].[Quotations] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_QuotationItems_Activities] FOREIGN KEY ([ActivityId]) REFERENCES [projects].[Activities] ([Id]) ON DELETE SET NULL,
        CONSTRAINT [FK_QuotationItems_ActivityGroups] FOREIGN KEY ([ActivityGroupId]) REFERENCES [projects].[ActivityGroups] ([Id]),
        CONSTRAINT [FK_QuotationItems_ActivityAlternativeParts] FOREIGN KEY ([AlternativePartId]) REFERENCES [projects].[ActivityAlternativeParts] ([Id]),
        CONSTRAINT [FK_QuotationItems_Currencies] FOREIGN KEY ([CurrencyId]) REFERENCES [dbo].[Currencies] ([Id])
    );
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
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_ProjectDocuments_ProjectId' AND object_id = OBJECT_ID(N'projects.ProjectDocuments')
)
    CREATE INDEX [IX_ProjectDocuments_ProjectId]
        ON [projects].[ProjectDocuments] ([ProjectId] ASC, [UploadedAt] DESC);
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
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_QuotationDocuments_QuotationId' AND object_id = OBJECT_ID(N'projects.QuotationDocuments')
)
    CREATE INDEX [IX_QuotationDocuments_QuotationId]
        ON [projects].[QuotationDocuments] ([QuotationId] ASC, [UploadedAt] DESC);
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'UX_Quotations_Code' AND object_id = OBJECT_ID(N'projects.Quotations')
)
    CREATE UNIQUE NONCLUSTERED INDEX [UX_Quotations_Code]
        ON [projects].[Quotations] ([Code] ASC);
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_Activities_ActivityGroupId' AND object_id = OBJECT_ID(N'projects.Activities')
)
    CREATE NONCLUSTERED INDEX [IX_Activities_ActivityGroupId]
        ON [projects].[Activities] ([ActivityGroupId] ASC)
        WHERE [ActivityGroupId] IS NOT NULL;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_QuotationItems_ActivityId_ActivityGroupId' AND object_id = OBJECT_ID(N'projects.QuotationItems')
)
    CREATE NONCLUSTERED INDEX [IX_QuotationItems_ActivityId_ActivityGroupId]
        ON [projects].[QuotationItems] ([ActivityId] ASC, [ActivityGroupId] ASC);
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_QuotationItems_PartNo' AND object_id = OBJECT_ID(N'projects.QuotationItems')
)
    CREATE NONCLUSTERED INDEX [IX_QuotationItems_PartNo]
        ON [projects].[QuotationItems] ([PartNo] ASC);
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_Projects_IsDeleted' AND object_id = OBJECT_ID(N'projects.Projects')
)
    CREATE NONCLUSTERED INDEX [IX_Projects_IsDeleted]
        ON [projects].[Projects] ([Id])
        INCLUDE ([Name], [Reference], [CreatedAt])
        WHERE [IsDeleted] = 0;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_Activities_IsDeleted' AND object_id = OBJECT_ID(N'projects.Activities')
)
    CREATE NONCLUSTERED INDEX [IX_Activities_IsDeleted]
        ON [projects].[Activities] ([ProjectId], [Id])
        INCLUDE ([DSNNo], [ActivityTypeId], [ActivityGroupId])
        WHERE [IsDeleted] = 0;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_Quotations_IsDeleted' AND object_id = OBJECT_ID(N'projects.Quotations')
)
    CREATE NONCLUSTERED INDEX [IX_Quotations_IsDeleted]
        ON [projects].[Quotations] ([Id])
        INCLUDE ([SupplierId], [Code], [CreatedAt])
        WHERE [IsDeleted] = 0;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_Suppliers_IsDeleted' AND object_id = OBJECT_ID(N'projects.Suppliers')
)
    CREATE NONCLUSTERED INDEX [IX_Suppliers_IsDeleted]
        ON [projects].[Suppliers] ([Id])
        INCLUDE ([Name], [Reference])
        WHERE [IsDeleted] = 0;
GO
