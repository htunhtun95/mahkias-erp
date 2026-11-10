CREATE TABLE [projects].[PurchaseOrderDocuments]
(
    [DocumentId] INT IDENTITY (1, 1) NOT NULL,
    [PurchaseOrderId] INT NOT NULL,
    [AssetId] UNIQUEIDENTIFIER NOT NULL,
    [IsOriginal] BIT CONSTRAINT [DF_PurchaseOrderDocuments_IsOriginal] DEFAULT (0) NOT NULL,
    CONSTRAINT [PK_PurchaseOrderDocuments] PRIMARY KEY CLUSTERED ([DocumentId] ASC),
    CONSTRAINT [FK_PurchaseOrderDocuments_PurchaseOrders] FOREIGN KEY ([PurchaseOrderId]) REFERENCES [projects].[PurchaseOrders] ([Id]),
    CONSTRAINT [FK_PurchaseOrderDocuments_Assets] FOREIGN KEY ([AssetId]) REFERENCES [media].[assets] ([id])
);
GO

CREATE INDEX [IX_PurchaseOrderDocuments_PurchaseOrderId]
    ON [projects].[PurchaseOrderDocuments] ([PurchaseOrderId] ASC, [AssetId] ASC);
