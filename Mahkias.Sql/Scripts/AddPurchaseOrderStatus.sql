IF OBJECT_ID(N'dbo.PurchaseOrderStatuses', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.PurchaseOrderStatuses
    (
        Id INT NOT NULL,
        Name NVARCHAR(50) NOT NULL,
        Code NVARCHAR(50) NOT NULL,
        SortOrder INT NOT NULL,
        CONSTRAINT PK_PurchaseOrderStatuses PRIMARY KEY CLUSTERED (Id)
    );
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'UX_PurchaseOrderStatuses_Code'
      AND object_id = OBJECT_ID(N'dbo.PurchaseOrderStatuses')
)
BEGIN
    CREATE UNIQUE INDEX UX_PurchaseOrderStatuses_Code
        ON dbo.PurchaseOrderStatuses (Code);
END
GO

IF NOT EXISTS (SELECT 1 FROM dbo.PurchaseOrderStatuses WHERE Id = 1)
    INSERT INTO dbo.PurchaseOrderStatuses (Id, Name, Code, SortOrder) VALUES (1, N'Draft', N'Draft', 1);
IF NOT EXISTS (SELECT 1 FROM dbo.PurchaseOrderStatuses WHERE Id = 2)
    INSERT INTO dbo.PurchaseOrderStatuses (Id, Name, Code, SortOrder) VALUES (2, N'Sent', N'Sent', 2);
IF NOT EXISTS (SELECT 1 FROM dbo.PurchaseOrderStatuses WHERE Id = 3)
    INSERT INTO dbo.PurchaseOrderStatuses (Id, Name, Code, SortOrder) VALUES (3, N'Revised', N'Revised', 3);
IF NOT EXISTS (SELECT 1 FROM dbo.PurchaseOrderStatuses WHERE Id = 4)
    INSERT INTO dbo.PurchaseOrderStatuses (Id, Name, Code, SortOrder) VALUES (4, N'Cancelled', N'Cancelled', 4);
GO

IF COL_LENGTH(N'projects.PurchaseOrders', N'StatusId') IS NULL
BEGIN
    ALTER TABLE projects.PurchaseOrders
        ADD StatusId INT NOT NULL
            CONSTRAINT DF_PurchaseOrders_StatusId DEFAULT (1);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_PurchaseOrders_Statuses')
BEGIN
    ALTER TABLE projects.PurchaseOrders
        ADD CONSTRAINT FK_PurchaseOrders_Statuses
            FOREIGN KEY (StatusId) REFERENCES dbo.PurchaseOrderStatuses (Id);
END
GO

IF OBJECT_ID(N'projects.PurchaseOrderHistory', N'U') IS NULL
BEGIN
    CREATE TABLE projects.PurchaseOrderHistory
    (
        Id INT IDENTITY (1, 1) NOT NULL,
        PurchaseOrderId INT NOT NULL,
        Action NVARCHAR(40) NOT NULL,
        Summary NVARCHAR(400) NOT NULL,
        StatusId INT NULL,
        ActorName NVARCHAR(200) NULL,
        ActorEmail NVARCHAR(320) NULL,
        UserId INT NULL,
        CreatedAt DATETIME CONSTRAINT DF_PurchaseOrderHistory_CreatedAt DEFAULT (GETDATE()) NOT NULL,
        CONSTRAINT PK_PurchaseOrderHistory PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_PurchaseOrderHistory_PurchaseOrders FOREIGN KEY (PurchaseOrderId) REFERENCES projects.PurchaseOrders (Id),
        CONSTRAINT FK_PurchaseOrderHistory_Statuses FOREIGN KEY (StatusId) REFERENCES dbo.PurchaseOrderStatuses (Id)
    );
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_PurchaseOrderHistory_PurchaseOrder'
      AND object_id = OBJECT_ID(N'projects.PurchaseOrderHistory')
)
BEGIN
    CREATE INDEX IX_PurchaseOrderHistory_PurchaseOrder
        ON projects.PurchaseOrderHistory (PurchaseOrderId, CreatedAt, Id);
END
GO
