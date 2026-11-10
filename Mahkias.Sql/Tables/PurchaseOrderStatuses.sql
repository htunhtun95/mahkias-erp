CREATE TABLE [dbo].[PurchaseOrderStatuses]
(
    [Id] INT NOT NULL,
    [Name] NVARCHAR(50) NOT NULL,
    [Code] NVARCHAR(50) NOT NULL,
    [SortOrder] INT NOT NULL,
    CONSTRAINT [PK_PurchaseOrderStatuses] PRIMARY KEY CLUSTERED ([Id] ASC)
);
GO

CREATE UNIQUE INDEX [UX_PurchaseOrderStatuses_Code]
    ON [dbo].[PurchaseOrderStatuses] ([Code] ASC);
GO
