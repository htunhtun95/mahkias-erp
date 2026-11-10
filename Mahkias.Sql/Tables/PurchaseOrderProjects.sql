CREATE TABLE [projects].[PurchaseOrderProjects]
(
    [PurchaseOrderId] INT NOT NULL,
    [ProjectId] INT NOT NULL,
    CONSTRAINT [PK_PurchaseOrderProjects] PRIMARY KEY CLUSTERED ([PurchaseOrderId] ASC, [ProjectId] ASC),
    CONSTRAINT [FK_PurchaseOrderProjects_PurchaseOrders] FOREIGN KEY ([PurchaseOrderId]) REFERENCES [projects].[PurchaseOrders] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_PurchaseOrderProjects_Projects] FOREIGN KEY ([ProjectId]) REFERENCES [projects].[Projects] ([Id])
);
GO

CREATE INDEX [IX_PurchaseOrderProjects_ProjectId]
    ON [projects].[PurchaseOrderProjects] ([ProjectId] ASC, [PurchaseOrderId] ASC);
