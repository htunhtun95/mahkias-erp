CREATE TABLE [projects].[PurchaseOrderCharges]
(
    [Id] INT IDENTITY (1, 1) NOT NULL,
    [PurchaseOrderId] INT NOT NULL,
    [Name] NVARCHAR(200) NOT NULL,
    [Basis] NVARCHAR(20) CONSTRAINT [DF_PurchaseOrderCharges_Basis] DEFAULT (N'Amount') NOT NULL,
    [Amount] DECIMAL(18, 2) NULL,
    [Rate] DECIMAL(9, 4) NULL,
    [BaseAmount] DECIMAL(18, 2) NOT NULL,
    [SortOrder] INT CONSTRAINT [DF_PurchaseOrderCharges_SortOrder] DEFAULT (0) NOT NULL,
    [CreatedAt] DATETIME CONSTRAINT [DF_PurchaseOrderCharges_CreatedAt] DEFAULT (GETDATE()) NOT NULL,
    CONSTRAINT [PK_PurchaseOrderCharges] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_PurchaseOrderCharges_PurchaseOrders] FOREIGN KEY ([PurchaseOrderId]) REFERENCES [projects].[PurchaseOrders] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [CK_PurchaseOrderCharges_Basis] CHECK ([Basis] IN (N'Amount', N'Percent'))
);
GO

CREATE INDEX [IX_PurchaseOrderCharges_PurchaseOrderId]
    ON [projects].[PurchaseOrderCharges] ([PurchaseOrderId] ASC, [SortOrder] ASC, [Id] ASC);
