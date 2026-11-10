IF OBJECT_ID(N'projects.PurchaseOrders', N'U') IS NULL
BEGIN
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
        [CreatedAt] DATETIME CONSTRAINT [DF_PurchaseOrders_CreatedAt] DEFAULT (GETDATE()) NOT NULL,
        [ModifiedAt] DATETIME NULL,
        [IsDeleted] BIT CONSTRAINT [DF_PurchaseOrders_IsDeleted] DEFAULT (0) NOT NULL,
        [DeletedAt] DATETIME2 NULL,
        [DeletedBy] NVARCHAR(100) NULL,
        CONSTRAINT [PK_PurchaseOrders] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_PurchaseOrders_Suppliers] FOREIGN KEY ([SupplierId]) REFERENCES [projects].[Suppliers] ([Id]),
        CONSTRAINT [FK_PurchaseOrders_Currencies] FOREIGN KEY ([CurrencyId]) REFERENCES [dbo].[Currencies] ([Id])
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_PurchaseOrders_Code' AND object_id = OBJECT_ID(N'projects.PurchaseOrders'))
BEGIN
    CREATE UNIQUE INDEX [UX_PurchaseOrders_Code]
        ON [projects].[PurchaseOrders] ([Code] ASC) WHERE [IsDeleted] = 0;
END
GO

IF OBJECT_ID(N'projects.PurchaseOrderProjects', N'U') IS NULL
BEGIN
    CREATE TABLE [projects].[PurchaseOrderProjects]
    (
        [PurchaseOrderId] INT NOT NULL,
        [ProjectId] INT NOT NULL,
        CONSTRAINT [PK_PurchaseOrderProjects] PRIMARY KEY CLUSTERED ([PurchaseOrderId] ASC, [ProjectId] ASC),
        CONSTRAINT [FK_PurchaseOrderProjects_PurchaseOrders] FOREIGN KEY ([PurchaseOrderId]) REFERENCES [projects].[PurchaseOrders] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_PurchaseOrderProjects_Projects] FOREIGN KEY ([ProjectId]) REFERENCES [projects].[Projects] ([Id])
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_PurchaseOrderProjects_ProjectId' AND object_id = OBJECT_ID(N'projects.PurchaseOrderProjects'))
BEGIN
    CREATE INDEX [IX_PurchaseOrderProjects_ProjectId]
        ON [projects].[PurchaseOrderProjects] ([ProjectId] ASC, [PurchaseOrderId] ASC);
END
GO

IF OBJECT_ID(N'projects.PurchaseOrderItems', N'U') IS NULL
BEGIN
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
        [LeadTime] NVARCHAR(100) NULL,
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
END
GO

IF EXISTS (
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'projects.PurchaseOrderItems')
      AND name = N'ActivityId'
      AND is_nullable = 0
)
BEGIN
    ALTER TABLE [projects].[PurchaseOrderItems] ALTER COLUMN [ActivityId] INT NULL;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_PurchaseOrderItems_PurchaseOrderId' AND object_id = OBJECT_ID(N'projects.PurchaseOrderItems'))
BEGIN
    CREATE INDEX [IX_PurchaseOrderItems_PurchaseOrderId]
        ON [projects].[PurchaseOrderItems] ([PurchaseOrderId] ASC, [SortOrder] ASC, [Id] ASC);
END
GO

IF OBJECT_ID(N'projects.PurchaseOrderCharges', N'U') IS NULL
BEGIN
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
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_PurchaseOrderCharges_PurchaseOrderId' AND object_id = OBJECT_ID(N'projects.PurchaseOrderCharges'))
BEGIN
    CREATE INDEX [IX_PurchaseOrderCharges_PurchaseOrderId]
        ON [projects].[PurchaseOrderCharges] ([PurchaseOrderId] ASC, [SortOrder] ASC, [Id] ASC);
END
GO

IF OBJECT_ID(N'projects.PurchaseOrderDocuments', N'U') IS NULL
BEGIN
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
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_PurchaseOrderDocuments_PurchaseOrderId' AND object_id = OBJECT_ID(N'projects.PurchaseOrderDocuments'))
BEGIN
    CREATE INDEX [IX_PurchaseOrderDocuments_PurchaseOrderId]
        ON [projects].[PurchaseOrderDocuments] ([PurchaseOrderId] ASC, [UploadedAt] DESC);
END
GO
