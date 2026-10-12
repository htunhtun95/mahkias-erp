IF COL_LENGTH(N'projects.QuotationItems', N'CurrencyId') IS NULL
BEGIN
    ALTER TABLE [projects].[QuotationItems] ADD [CurrencyId] INT NULL;
END
GO

IF COL_LENGTH(N'projects.QuotationItems', N'ExchangeRate') IS NULL
BEGIN
    ALTER TABLE [projects].[QuotationItems] ADD [ExchangeRate] DECIMAL(18, 6) NULL;
END
GO

IF COL_LENGTH(N'projects.QuotationItems', N'BaseUnitPrice') IS NULL
BEGIN
    ALTER TABLE [projects].[QuotationItems] ADD [BaseUnitPrice] DECIMAL(18, 2) NULL;
END
GO

IF COL_LENGTH(N'projects.QuotationItems', N'BaseTotalPrice') IS NULL
BEGIN
    ALTER TABLE [projects].[QuotationItems] ADD [BaseTotalPrice] DECIMAL(18, 2) NULL;
END
GO

UPDATE I
SET CurrencyId = Q.CurrencyId
FROM [projects].[QuotationItems] AS I
INNER JOIN [projects].[Quotations] AS Q ON Q.Id = I.QuotationId
WHERE I.CurrencyId IS NULL;
GO

UPDATE I
SET CurrencyId = C.Id
FROM [projects].[QuotationItems] AS I
CROSS JOIN [projects].[Currencies] AS C
WHERE C.Code = N'USD'
  AND I.CurrencyId IS NULL;
GO

UPDATE [projects].[QuotationItems]
SET ExchangeRate = 1.0
WHERE ExchangeRate IS NULL;
GO

UPDATE [projects].[QuotationItems]
SET BaseUnitPrice = ROUND(UnitPrice, 2)
WHERE BaseUnitPrice IS NULL
  AND UnitPrice IS NOT NULL;
GO

UPDATE [projects].[QuotationItems]
SET BaseTotalPrice = ROUND(TotalPrice, 2)
WHERE BaseTotalPrice IS NULL
  AND TotalPrice IS NOT NULL;
GO

ALTER TABLE [projects].[QuotationItems] ALTER COLUMN [UnitPrice] DECIMAL(18, 2) NULL;
GO

ALTER TABLE [projects].[QuotationItems] ALTER COLUMN [TotalPrice] DECIMAL(18, 2) NULL;
GO

ALTER TABLE [projects].[QuotationItems] ALTER COLUMN [CurrencyId] INT NOT NULL;
GO

ALTER TABLE [projects].[QuotationItems] ALTER COLUMN [ExchangeRate] DECIMAL(18, 6) NOT NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.default_constraints WHERE name = N'DF_QuotationItems_ExchangeRate')
BEGIN
    ALTER TABLE [projects].[QuotationItems] ADD CONSTRAINT [DF_QuotationItems_ExchangeRate] DEFAULT (1.0) FOR [ExchangeRate];
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.default_constraints WHERE name = N'DF_QuotationItems_CurrencyId')
BEGIN
    DECLARE @UsdId INT = (SELECT Id FROM [projects].[Currencies] WHERE Code = N'USD');
    DECLARE @Sql NVARCHAR(400) = N'ALTER TABLE [projects].[QuotationItems] ADD CONSTRAINT [DF_QuotationItems_CurrencyId] DEFAULT (' + CAST(@UsdId AS NVARCHAR(12)) + N') FOR [CurrencyId];';
    EXEC sys.sp_executesql @Sql;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_QuotationItems_Currencies')
BEGIN
    ALTER TABLE [projects].[QuotationItems] ADD CONSTRAINT [FK_QuotationItems_Currencies]
        FOREIGN KEY ([CurrencyId]) REFERENCES [projects].[Currencies] ([Id]);
END
GO

UPDATE Q
SET BaseTotalAmount = ROUND(ISNULL((
    SELECT SUM(I.BaseTotalPrice)
    FROM [projects].[QuotationItems] AS I
    WHERE I.QuotationId = Q.Id
), 0), 2)
FROM [projects].[Quotations] AS Q;
GO
