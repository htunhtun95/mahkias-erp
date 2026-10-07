CREATE TABLE [projects].[Projects]
(
    [Id] INT IDENTITY (1, 1) NOT NULL,
    [Name] NVARCHAR(250) NOT NULL,
    [Reference] NVARCHAR(100) NULL,
    [Description] NVARCHAR(MAX) NULL,
    [CreatedAt] DATETIME CONSTRAINT [DF_Projects_CreatedAt] DEFAULT (GETDATE()) NOT NULL,
    [ModifiedAt] DATETIME NULL,
    [IsDeleted] BIT CONSTRAINT [DF_Projects_IsDeleted] DEFAULT (0) NOT NULL,
    [DeletedAt] DATETIME2 NULL,
    [DeletedBy] NVARCHAR(100) NULL,
    CONSTRAINT [PK_Projects] PRIMARY KEY CLUSTERED ([Id] ASC)
);
