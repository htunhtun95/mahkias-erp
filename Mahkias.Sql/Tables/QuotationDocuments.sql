CREATE TABLE [projects].[QuotationDocuments]
(
    [DocumentId] INT IDENTITY (1, 1) NOT NULL,
    [QuotationId] INT NOT NULL,
    [AssetId] UNIQUEIDENTIFIER NOT NULL,
    [IsOriginal] BIT CONSTRAINT [DF_QuotationDocuments_IsOriginal] DEFAULT (0) NOT NULL,
    CONSTRAINT [PK_QuotationDocuments] PRIMARY KEY CLUSTERED ([DocumentId] ASC),
    CONSTRAINT [FK_QuotationDocuments_Quotations] FOREIGN KEY ([QuotationId]) REFERENCES [projects].[Quotations] ([Id]),
    CONSTRAINT [FK_QuotationDocuments_Assets] FOREIGN KEY ([AssetId]) REFERENCES [media].[assets] ([id])
);
GO

CREATE INDEX [IX_QuotationDocuments_QuotationId]
    ON [projects].[QuotationDocuments] ([QuotationId] ASC, [AssetId] ASC);
