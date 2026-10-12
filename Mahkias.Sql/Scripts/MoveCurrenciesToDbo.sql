IF OBJECT_ID(N'dbo.Currencies', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[Currencies]
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
      AND object_id = OBJECT_ID(N'dbo.Currencies')
)
BEGIN
    CREATE UNIQUE INDEX [UX_Currencies_SingleDefault]
        ON [dbo].[Currencies] ([IsDefault])
        WHERE [IsDefault] = 1;
END
GO

IF NOT EXISTS (SELECT 1 FROM [dbo].[Currencies])
BEGIN
    INSERT INTO [dbo].[Currencies] (Code, Name, Symbol, IsDefault, IsActive)
    VALUES
        (N'USD', N'US Dollar', N'$', 1, 1),
        (N'EUR', N'Euro', NCHAR(8364), 0, 1),
        (N'GBP', N'Pound Sterling', NCHAR(163), 0, 1),
        (N'THB', N'Thai Baht', NCHAR(3647), 0, 1),
        (N'MYR', N'Malaysian Ringgit', N'RM', 0, 1);
END
GO

IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Quotations_Currencies')
BEGIN
    ALTER TABLE [projects].[Quotations] DROP CONSTRAINT [FK_Quotations_Currencies];
END
GO

IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_QuotationItems_Currencies')
BEGIN
    ALTER TABLE [projects].[QuotationItems] DROP CONSTRAINT [FK_QuotationItems_Currencies];
END
GO

IF OBJECT_ID(N'projects.Currencies', N'U') IS NOT NULL
BEGIN
    UPDATE Q
    SET CurrencyId = New.Id
    FROM [projects].[Quotations] AS Q
    INNER JOIN [projects].[Currencies] AS Old ON Old.Id = Q.CurrencyId
    INNER JOIN [dbo].[Currencies] AS New ON New.Code = Old.Code;

    UPDATE I
    SET CurrencyId = New.Id
    FROM [projects].[QuotationItems] AS I
    INNER JOIN [projects].[Currencies] AS Old ON Old.Id = I.CurrencyId
    INNER JOIN [dbo].[Currencies] AS New ON New.Code = Old.Code;
END
GO

IF OBJECT_ID(N'dbo.ExchangeRates', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[ExchangeRates]
    (
        [ExchangeRateId] INT IDENTITY (1, 1) NOT NULL,
        [FromCurrencyId] INT NOT NULL,
        [ToCurrencyId] INT NOT NULL,
        [Rate] DECIMAL(18, 6) NOT NULL,
        [EffectiveDate] DATE NOT NULL,
        CONSTRAINT [PK_ExchangeRates] PRIMARY KEY CLUSTERED ([ExchangeRateId] ASC),
        CONSTRAINT [FK_ExchangeRates_FromCurrency] FOREIGN KEY ([FromCurrencyId]) REFERENCES [dbo].[Currencies] ([Id]),
        CONSTRAINT [FK_ExchangeRates_ToCurrency] FOREIGN KEY ([ToCurrencyId]) REFERENCES [dbo].[Currencies] ([Id]),
        CONSTRAINT [UX_ExchangeRates_PairDate] UNIQUE ([FromCurrencyId], [ToCurrencyId], [EffectiveDate]),
        CONSTRAINT [CK_ExchangeRates_DistinctPair] CHECK ([FromCurrencyId] <> [ToCurrencyId]),
        CONSTRAINT [CK_ExchangeRates_Rate] CHECK ([Rate] > 0)
    );
END
GO

IF OBJECT_ID(N'projects.ExchangeRates', N'U') IS NOT NULL
   AND OBJECT_ID(N'projects.Currencies', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM [dbo].[ExchangeRates])
BEGIN
    INSERT INTO [dbo].[ExchangeRates] (FromCurrencyId, ToCurrencyId, Rate, EffectiveDate)
    SELECT FromNew.Id, ToNew.Id, Rate.Rate, Rate.EffectiveDate
    FROM [projects].[ExchangeRates] AS Rate
    INNER JOIN [projects].[Currencies] AS FromOld ON FromOld.Id = Rate.FromCurrencyId
    INNER JOIN [projects].[Currencies] AS ToOld ON ToOld.Id = Rate.ToCurrencyId
    INNER JOIN [dbo].[Currencies] AS FromNew ON FromNew.Code = FromOld.Code
    INNER JOIN [dbo].[Currencies] AS ToNew ON ToNew.Code = ToOld.Code;
END
GO

IF OBJECT_ID(N'projects.ExchangeRates', N'U') IS NOT NULL
BEGIN
    DROP TABLE [projects].[ExchangeRates];
END
GO

IF OBJECT_ID(N'projects.Currencies', N'U') IS NOT NULL
BEGIN
    DROP TABLE [projects].[Currencies];
END
GO

IF EXISTS (SELECT 1 FROM sys.default_constraints WHERE name = N'DF_Quotations_CurrencyId')
BEGIN
    ALTER TABLE [projects].[Quotations] DROP CONSTRAINT [DF_Quotations_CurrencyId];
END
GO

IF EXISTS (SELECT 1 FROM sys.default_constraints WHERE name = N'DF_QuotationItems_CurrencyId')
BEGIN
    ALTER TABLE [projects].[QuotationItems] DROP CONSTRAINT [DF_QuotationItems_CurrencyId];
END
GO

DECLARE @UsdId INT = (SELECT Id FROM [dbo].[Currencies] WHERE Code = N'USD');
IF @UsdId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.default_constraints WHERE name = N'DF_Quotations_CurrencyId')
BEGIN
    DECLARE @QuoteDefault NVARCHAR(400) = N'ALTER TABLE [projects].[Quotations] ADD CONSTRAINT [DF_Quotations_CurrencyId] DEFAULT (' + CAST(@UsdId AS NVARCHAR(12)) + N') FOR [CurrencyId];';
    EXEC sys.sp_executesql @QuoteDefault;
END
IF @UsdId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.default_constraints WHERE name = N'DF_QuotationItems_CurrencyId')
BEGIN
    DECLARE @ItemDefault NVARCHAR(400) = N'ALTER TABLE [projects].[QuotationItems] ADD CONSTRAINT [DF_QuotationItems_CurrencyId] DEFAULT (' + CAST(@UsdId AS NVARCHAR(12)) + N') FOR [CurrencyId];';
    EXEC sys.sp_executesql @ItemDefault;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Quotations_Currencies')
BEGIN
    ALTER TABLE [projects].[Quotations] ADD CONSTRAINT [FK_Quotations_Currencies]
        FOREIGN KEY ([CurrencyId]) REFERENCES [dbo].[Currencies] ([Id]);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_QuotationItems_Currencies')
BEGIN
    ALTER TABLE [projects].[QuotationItems] ADD CONSTRAINT [FK_QuotationItems_Currencies]
        FOREIGN KEY ([CurrencyId]) REFERENCES [dbo].[Currencies] ([Id]);
END
GO
