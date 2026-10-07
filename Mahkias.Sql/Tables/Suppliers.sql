CREATE TABLE [projects].[Suppliers]
(
    [Id] INT IDENTITY (1, 1) NOT NULL,
    [Name] NVARCHAR(250) NOT NULL,
    [Reference] NVARCHAR(100) NULL,
    [CreatedAt] DATETIME CONSTRAINT [DF_Suppliers_CreatedAt] DEFAULT (GETDATE()) NOT NULL,
    [ModifiedAt] DATETIME NULL,
    [IsDeleted] BIT CONSTRAINT [DF_Suppliers_IsDeleted] DEFAULT (0) NOT NULL,
    [DeletedAt] DATETIME2 NULL,
    [DeletedBy] NVARCHAR(100) NULL,
    CONSTRAINT [PK_Suppliers] PRIMARY KEY CLUSTERED ([Id] ASC)
);
