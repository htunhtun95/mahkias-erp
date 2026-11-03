CREATE TABLE [projects].[PurchaseOrderDocuments]
(
    [DocumentId] INT IDENTITY (1, 1) NOT NULL,
    [PurchaseOrderId] INT NOT NULL,
    [FileName] NVARCHAR(260) NOT NULL,
    [FileType] NVARCHAR(100) NOT NULL,
    [GoogleDriveFileId] NVARCHAR(128) NOT NULL,
    [WebViewLink] NVARCHAR(1000) NOT NULL,
    [UploadedAt] DATETIME2 CONSTRAINT [DF_PurchaseOrderDocuments_UploadedAt] DEFAULT (SYSUTCDATETIME()) NOT NULL,
    CONSTRAINT [PK_PurchaseOrderDocuments] PRIMARY KEY CLUSTERED ([DocumentId] ASC),
    CONSTRAINT [FK_PurchaseOrderDocuments_PurchaseOrders] FOREIGN KEY ([PurchaseOrderId]) REFERENCES [projects].[PurchaseOrders] ([Id])
);
GO

CREATE INDEX [IX_PurchaseOrderDocuments_PurchaseOrderId]
    ON [projects].[PurchaseOrderDocuments] ([PurchaseOrderId] ASC, [UploadedAt] DESC);
