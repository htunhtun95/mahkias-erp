CREATE TABLE [projects].[PurchaseOrders]
(
    [Id] INT IDENTITY (1, 1) NOT NULL,
    [SupplierId] INT NOT NULL,
    [Code] NVARCHAR(50) NOT NULL,
    [PoDate] DATETIME NULL,
    [ShipToAddress] NVARCHAR(500) NULL,
    [CurrencyId] INT NOT NULL,
    [ExchangeRate] DECIMAL(18, 6) CONSTRAINT [DF_PurchaseOrders_ExchangeRate] DEFAULT (1.0) NOT NULL,
    [SubTotal] DECIMAL(18, 2) CONSTRAINT [DF_PurchaseOrders_SubTotal] DEFAULT (0) NOT NULL,
    [BaseSubTotal] DECIMAL(18, 2) CONSTRAINT [DF_PurchaseOrders_BaseSubTotal] DEFAULT (0) NOT NULL,
    [Total] DECIMAL(18, 2) CONSTRAINT [DF_PurchaseOrders_Total] DEFAULT (0) NOT NULL,
    [BaseTotalAmount] DECIMAL(18, 2) CONSTRAINT [DF_PurchaseOrders_BaseTotalAmount] DEFAULT (0) NOT NULL,
    [StatusId] INT CONSTRAINT [DF_PurchaseOrders_StatusId] DEFAULT (1) NOT NULL,
    [CreatedAt] DATETIME CONSTRAINT [DF_PurchaseOrders_CreatedAt] DEFAULT (GETDATE()) NOT NULL,
    [ModifiedAt] DATETIME NULL,
    [IsDeleted] BIT CONSTRAINT [DF_PurchaseOrders_IsDeleted] DEFAULT (0) NOT NULL,
    [DeletedAt] DATETIME2 NULL,
    [DeletedBy] NVARCHAR(100) NULL,
    CONSTRAINT [PK_PurchaseOrders] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_PurchaseOrders_Suppliers] FOREIGN KEY ([SupplierId]) REFERENCES [projects].[Suppliers] ([Id]),
    CONSTRAINT [FK_PurchaseOrders_Currencies] FOREIGN KEY ([CurrencyId]) REFERENCES [dbo].[Currencies] ([Id]),
    CONSTRAINT [FK_PurchaseOrders_Statuses] FOREIGN KEY ([StatusId]) REFERENCES [dbo].[PurchaseOrderStatuses] ([Id])
);
GO

CREATE UNIQUE INDEX [UX_PurchaseOrders_Code]
    ON [projects].[PurchaseOrders] ([Code] ASC) WHERE [IsDeleted] = 0;
