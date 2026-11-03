CREATE TABLE [projects].[QuotationCharges]
(
    [Id] INT IDENTITY (1, 1) NOT NULL,
    [QuotationId] INT NOT NULL,
    [Name] NVARCHAR(200) NOT NULL,
    [Basis] NVARCHAR(20) CONSTRAINT [DF_QuotationCharges_Basis] DEFAULT (N'Amount') NOT NULL,
    [Amount] DECIMAL(18, 2) NULL,
    [Rate] DECIMAL(9, 4) NULL,
    [BaseAmount] DECIMAL(18, 2) NOT NULL,
    [SortOrder] INT CONSTRAINT [DF_QuotationCharges_SortOrder] DEFAULT (0) NOT NULL,
    [CreatedAt] DATETIME CONSTRAINT [DF_QuotationCharges_CreatedAt] DEFAULT (GETDATE()) NOT NULL,
    CONSTRAINT [PK_QuotationCharges] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_QuotationCharges_Quotations] FOREIGN KEY ([QuotationId]) REFERENCES [projects].[Quotations] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [CK_QuotationCharges_Basis] CHECK ([Basis] IN (N'Amount', N'Percent'))
);
GO

CREATE INDEX [IX_QuotationCharges_QuotationId]
    ON [projects].[QuotationCharges] ([QuotationId] ASC, [SortOrder] ASC, [Id] ASC);
