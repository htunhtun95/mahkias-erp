IF OBJECT_ID(N'projects.Currencies', N'U') IS NULL
BEGIN
    CREATE TABLE [projects].[Currencies]
    (
        [Id] INT IDENTITY (1, 1) NOT NULL,
        [Code] NVARCHAR(3) NOT NULL,
        [Name] NVARCHAR(100) NOT NULL,
        [Symbol] NVARCHAR(10) NOT NULL,
        [IsDefault] BIT CONSTRAINT [DF_Currencies_IsDefault] DEFAULT (0) NOT NULL,
        [IsActive] BIT CONSTRAINT [DF_Currencies_IsActive] DEFAULT (1) NOT NULL,
        CONSTRAINT [PK_Currencies] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [UX_Currencies_Code] UNIQUE ([Code] ASC)
    );
END
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'UX_Currencies_SingleDefault'
      AND object_id = OBJECT_ID(N'projects.Currencies')
)
BEGIN
    CREATE UNIQUE INDEX [UX_Currencies_SingleDefault]
        ON [projects].[Currencies] ([IsDefault])
        WHERE [IsDefault] = 1;
END
GO

MERGE [projects].[Currencies] AS Target
USING (VALUES
    (N'USD', N'US Dollar', N'$', 1),
    (N'EUR', N'Euro', NCHAR(8364), 0),
    (N'GBP', N'Pound Sterling', NCHAR(163), 0),
    (N'THB', N'Thai Baht', NCHAR(3647), 0),
    (N'MYR', N'Malaysian Ringgit', N'RM', 0)
) AS Source (Code, Name, Symbol, IsDefault)
ON Target.Code = Source.Code
WHEN MATCHED THEN
    UPDATE SET
        Name = Source.Name,
        Symbol = Source.Symbol,
        IsDefault = Source.IsDefault,
        IsActive = 1
WHEN NOT MATCHED THEN
    INSERT (Code, Name, Symbol, IsDefault, IsActive)
    VALUES (Source.Code, Source.Name, Source.Symbol, Source.IsDefault, 1);
GO

IF OBJECT_ID(N'projects.ExchangeRates', N'U') IS NULL
BEGIN
    CREATE TABLE [projects].[ExchangeRates]
    (
        [ExchangeRateId] INT IDENTITY (1, 1) NOT NULL,
        [FromCurrencyId] INT NOT NULL,
        [ToCurrencyId] INT NOT NULL,
        [Rate] DECIMAL(18, 6) NOT NULL,
        [EffectiveDate] DATE NOT NULL,
        CONSTRAINT [PK_ExchangeRates] PRIMARY KEY CLUSTERED ([ExchangeRateId] ASC),
        CONSTRAINT [FK_ExchangeRates_FromCurrency] FOREIGN KEY ([FromCurrencyId]) REFERENCES [projects].[Currencies] ([Id]),
        CONSTRAINT [FK_ExchangeRates_ToCurrency] FOREIGN KEY ([ToCurrencyId]) REFERENCES [projects].[Currencies] ([Id]),
        CONSTRAINT [UX_ExchangeRates_PairDate] UNIQUE ([FromCurrencyId], [ToCurrencyId], [EffectiveDate]),
        CONSTRAINT [CK_ExchangeRates_DistinctPair] CHECK ([FromCurrencyId] <> [ToCurrencyId]),
        CONSTRAINT [CK_ExchangeRates_Rate] CHECK ([Rate] > 0)
    );
END
GO

IF COL_LENGTH(N'projects.Quotations', N'CurrencyId') IS NULL
BEGIN
    ALTER TABLE [projects].[Quotations] ADD [CurrencyId] INT NULL;
END
GO

IF COL_LENGTH(N'projects.Quotations', N'ExchangeRate') IS NULL
BEGIN
    ALTER TABLE [projects].[Quotations] ADD [ExchangeRate] DECIMAL(18, 6) NULL;
END
GO

IF COL_LENGTH(N'projects.Quotations', N'BaseTotalAmount') IS NULL
BEGIN
    ALTER TABLE [projects].[Quotations] ADD [BaseTotalAmount] DECIMAL(18, 2) NULL;
END
GO

UPDATE Q
SET CurrencyId = C.Id
FROM [projects].[Quotations] AS Q
CROSS JOIN [projects].[Currencies] AS C
WHERE C.Code = N'USD'
  AND Q.CurrencyId IS NULL;
GO

UPDATE [projects].[Quotations]
SET ExchangeRate = 1.0
WHERE ExchangeRate IS NULL;
GO

UPDATE Q
SET BaseTotalAmount = ROUND(ISNULL(Totals.Amount, 0), 2)
FROM [projects].[Quotations] AS Q
OUTER APPLY (
    SELECT SUM(I.TotalPrice) AS Amount
    FROM [projects].[QuotationItems] AS I
    WHERE I.QuotationId = Q.Id
) AS Totals
WHERE Q.BaseTotalAmount IS NULL;
GO

ALTER TABLE [projects].[Quotations] ALTER COLUMN [CurrencyId] INT NOT NULL;
GO

ALTER TABLE [projects].[Quotations] ALTER COLUMN [ExchangeRate] DECIMAL(18, 6) NOT NULL;
GO

ALTER TABLE [projects].[Quotations] ALTER COLUMN [BaseTotalAmount] DECIMAL(18, 2) NOT NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.default_constraints WHERE name = N'DF_Quotations_ExchangeRate')
BEGIN
    ALTER TABLE [projects].[Quotations] ADD CONSTRAINT [DF_Quotations_ExchangeRate] DEFAULT (1.0) FOR [ExchangeRate];
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.default_constraints WHERE name = N'DF_Quotations_BaseTotalAmount')
BEGIN
    ALTER TABLE [projects].[Quotations] ADD CONSTRAINT [DF_Quotations_BaseTotalAmount] DEFAULT (0) FOR [BaseTotalAmount];
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.default_constraints WHERE name = N'DF_Quotations_CurrencyId')
BEGIN
    DECLARE @UsdId INT = (SELECT Id FROM [projects].[Currencies] WHERE Code = N'USD');
    DECLARE @Sql NVARCHAR(400) = N'ALTER TABLE [projects].[Quotations] ADD CONSTRAINT [DF_Quotations_CurrencyId] DEFAULT (' + CAST(@UsdId AS NVARCHAR(12)) + N') FOR [CurrencyId];';
    EXEC sys.sp_executesql @Sql;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Quotations_Currencies')
BEGIN
    ALTER TABLE [projects].[Quotations] ADD CONSTRAINT [FK_Quotations_Currencies]
        FOREIGN KEY ([CurrencyId]) REFERENCES [projects].[Currencies] ([Id]);
END
GO
