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
