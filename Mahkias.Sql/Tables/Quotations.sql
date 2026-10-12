CREATE TABLE [projects].[Quotations]
(
    [Id] INT IDENTITY (1, 1) NOT NULL,
    [SupplierId] INT NOT NULL,
    [Code] NVARCHAR(50) NOT NULL,
    [SupplierReference] NVARCHAR(100) NULL,
    [DriveFileLink] NVARCHAR(1000) NULL,
    [CurrencyId] INT NOT NULL,
    [ExchangeRate] DECIMAL(18, 6) CONSTRAINT [DF_Quotations_ExchangeRate] DEFAULT (1.0) NOT NULL,
    [BaseTotalAmount] DECIMAL(18, 2) CONSTRAINT [DF_Quotations_BaseTotalAmount] DEFAULT (0) NOT NULL,
    [CreatedAt] DATETIME CONSTRAINT [DF_Quotations_CreatedAt] DEFAULT (GETDATE()) NOT NULL,
    [ModifiedAt] DATETIME NULL,
    [IsDeleted] BIT CONSTRAINT [DF_Quotations_IsDeleted] DEFAULT (0) NOT NULL,
    [DeletedAt] DATETIME2 NULL,
    [DeletedBy] NVARCHAR(100) NULL,
    CONSTRAINT [PK_Quotations] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_Quotations_Suppliers] FOREIGN KEY ([SupplierId]) REFERENCES [projects].[Suppliers] ([Id]),
    CONSTRAINT [FK_Quotations_Currencies] FOREIGN KEY ([CurrencyId]) REFERENCES [dbo].[Currencies] ([Id])
);
