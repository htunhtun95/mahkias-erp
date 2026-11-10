CREATE TABLE [projects].[PurchaseOrderHistory]
(
    [Id] INT IDENTITY (1, 1) NOT NULL,
    [PurchaseOrderId] INT NOT NULL,
    [Action] NVARCHAR(40) NOT NULL,
    [Summary] NVARCHAR(400) NOT NULL,
    [StatusId] INT NULL,
    [ActorName] NVARCHAR(200) NULL,
    [ActorEmail] NVARCHAR(320) NULL,
    -- Reserved for a future users table. No foreign key until that table exists.
    [UserId] INT NULL,
    [CreatedAt] DATETIME CONSTRAINT [DF_PurchaseOrderHistory_CreatedAt] DEFAULT (GETDATE()) NOT NULL,
    CONSTRAINT [PK_PurchaseOrderHistory] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_PurchaseOrderHistory_PurchaseOrders] FOREIGN KEY ([PurchaseOrderId]) REFERENCES [projects].[PurchaseOrders] ([Id]),
    CONSTRAINT [FK_PurchaseOrderHistory_Statuses] FOREIGN KEY ([StatusId]) REFERENCES [dbo].[PurchaseOrderStatuses] ([Id])
);
GO

CREATE INDEX [IX_PurchaseOrderHistory_PurchaseOrder]
    ON [projects].[PurchaseOrderHistory] ([PurchaseOrderId] ASC, [CreatedAt] ASC, [Id] ASC);
GO
