CREATE TABLE [projects].[PurchaseOrderItems]
(
    [Id] INT IDENTITY (1, 1) NOT NULL,
    [PurchaseOrderId] INT NOT NULL,
    [ActivityId] INT NULL,
    [QuotationId] INT NULL,
    [DsnNo] NVARCHAR(100) NULL,
    [PartNo] NVARCHAR(200) NULL,
    [PartDescription] NVARCHAR(500) NULL,
    [Condition] NVARCHAR(100) NULL,
    [Unit] NVARCHAR(100) NULL,
    [LeadTime] INT NULL,
    [Quantity] DECIMAL(18, 2) NOT NULL,
    [UnitPrice] DECIMAL(18, 2) NOT NULL,
    [TotalPrice] DECIMAL(18, 2) NOT NULL,
    [CurrencyId] INT NOT NULL,
    [ExchangeRate] DECIMAL(18, 6) NOT NULL,
    [BaseUnitPrice] DECIMAL(18, 2) NOT NULL,
    [BaseTotalPrice] DECIMAL(18, 2) NOT NULL,
    [SortOrder] INT CONSTRAINT [DF_PurchaseOrderItems_SortOrder] DEFAULT (0) NOT NULL,
    CONSTRAINT [PK_PurchaseOrderItems] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_PurchaseOrderItems_PurchaseOrders] FOREIGN KEY ([PurchaseOrderId]) REFERENCES [projects].[PurchaseOrders] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_PurchaseOrderItems_Activities] FOREIGN KEY ([ActivityId]) REFERENCES [projects].[Activities] ([Id]),
    CONSTRAINT [FK_PurchaseOrderItems_Quotations] FOREIGN KEY ([QuotationId]) REFERENCES [projects].[Quotations] ([Id]),
    CONSTRAINT [FK_PurchaseOrderItems_Currencies] FOREIGN KEY ([CurrencyId]) REFERENCES [dbo].[Currencies] ([Id])
);
GO

CREATE INDEX [IX_PurchaseOrderItems_PurchaseOrderId]
    ON [projects].[PurchaseOrderItems] ([PurchaseOrderId] ASC, [SortOrder] ASC, [Id] ASC);
