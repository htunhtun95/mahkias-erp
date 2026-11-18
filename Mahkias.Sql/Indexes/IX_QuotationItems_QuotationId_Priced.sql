CREATE NONCLUSTERED INDEX [IX_QuotationItems_QuotationId_Priced]
    ON [projects].[QuotationItems]([QuotationId] ASC)
    INCLUDE([DsnNo], [PartNo], [UnitPrice], [PartDescription], [Condition], [LeadTime], [MinimumQty], [Quantity], [TotalPrice], [BaseUnitPrice], [BaseTotalPrice], [CurrencyId], [UnitId], [CreatedAt], [ModifiedAt])
    WHERE [UnitPrice] IS NOT NULL;
