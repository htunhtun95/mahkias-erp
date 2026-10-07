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
    [UnitPrice] DECIMAL(18, 4) NULL,
    [Quantity] INT CONSTRAINT [DF_QuotationItems_Quantity] DEFAULT (1) NOT NULL,
    [TotalPrice] DECIMAL(18, 4) NULL,
    [AwardedQuantity] INT CONSTRAINT [DF_QuotationItems_AwardedQuantity] DEFAULT (0) NOT NULL,
    [CreatedAt] DATETIME CONSTRAINT [DF_QuotationItems_CreatedAt] DEFAULT (GETDATE()) NOT NULL,
    [ModifiedAt] DATETIME NULL,
    CONSTRAINT [PK_QuotationItems] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_QuotationItems_Quotations] FOREIGN KEY ([QuotationId]) REFERENCES [projects].[Quotations] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_QuotationItems_Activities] FOREIGN KEY ([ActivityId]) REFERENCES [projects].[Activities] ([Id]) ON DELETE SET NULL,
    CONSTRAINT [FK_QuotationItems_ActivityGroups] FOREIGN KEY ([ActivityGroupId]) REFERENCES [projects].[ActivityGroups] ([Id]),
    CONSTRAINT [FK_QuotationItems_ActivityAlternativeParts] FOREIGN KEY ([AlternativePartId]) REFERENCES [projects].[ActivityAlternativeParts] ([Id])
);
