IF OBJECT_ID(N'projects.Suppliers', N'U') IS NULL
BEGIN
    CREATE TABLE [projects].[Suppliers]
    (
        [Id] INT IDENTITY (1, 1) NOT NULL,
        [Name] NVARCHAR(250) NOT NULL,
        [Reference] NVARCHAR(100) NULL,
        [CreatedAt] DATETIME CONSTRAINT [DF_Suppliers_CreatedAt] DEFAULT (GETDATE()) NOT NULL,
        [ModifiedAt] DATETIME NULL,
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
        [CreatedAt] DATETIME CONSTRAINT [DF_Quotations_CreatedAt] DEFAULT (GETDATE()) NOT NULL,
        [ModifiedAt] DATETIME NULL,
        CONSTRAINT [PK_Quotations] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_Quotations_Suppliers] FOREIGN KEY ([SupplierId]) REFERENCES [projects].[Suppliers] ([Id])
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
        [IsAlternativePart] BIT CONSTRAINT [DF_QuotationItems_IsAlternativePart] DEFAULT (0) NOT NULL,
        [AlternativePartId] INT NULL,
        [PartNo] NVARCHAR(100) NULL,
        [PartDescription] NVARCHAR(500) NULL,
        [UnitPrice] DECIMAL(18, 4) NOT NULL,
        [Quantity] INT CONSTRAINT [DF_QuotationItems_Quantity] DEFAULT (1) NOT NULL,
        [TotalPrice] DECIMAL(18, 4) NOT NULL,
        [AwardedQuantity] INT CONSTRAINT [DF_QuotationItems_AwardedQuantity] DEFAULT (0) NOT NULL,
        [CreatedAt] DATETIME CONSTRAINT [DF_QuotationItems_CreatedAt] DEFAULT (GETDATE()) NOT NULL,
        [ModifiedAt] DATETIME NULL,
        CONSTRAINT [PK_QuotationItems] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_QuotationItems_Quotations] FOREIGN KEY ([QuotationId]) REFERENCES [projects].[Quotations] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_QuotationItems_Activities] FOREIGN KEY ([ActivityId]) REFERENCES [projects].[Activities] ([Id]) ON DELETE SET NULL,
        CONSTRAINT [FK_QuotationItems_ActivityAlternativeParts] FOREIGN KEY ([AlternativePartId]) REFERENCES [projects].[ActivityAlternativeParts] ([Id])
    );

    CREATE NONCLUSTERED INDEX [IX_QuotationItems_PartNo]
        ON [projects].[QuotationItems] ([PartNo] ASC);
END
GO
