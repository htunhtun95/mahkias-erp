-- Speeds the quotation-links lookup. Safe to run more than once. No rows are changed.
IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_ActivityAlternativeParts_ActivityId'
      AND object_id = OBJECT_ID(N'projects.ActivityAlternativeParts')
)
BEGIN
    CREATE INDEX IX_ActivityAlternativeParts_ActivityId
        ON projects.ActivityAlternativeParts (ActivityId)
        INCLUDE (MainPartNo, AlternativePartNo);
END
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_QuotationItems_QuotationId_Priced'
      AND object_id = OBJECT_ID(N'projects.QuotationItems')
)
BEGIN
    CREATE INDEX IX_QuotationItems_QuotationId_Priced
        ON projects.QuotationItems (QuotationId)
        INCLUDE (DsnNo, PartNo, UnitPrice, PartDescription, Condition, LeadTime, MinimumQty, Quantity, TotalPrice, BaseUnitPrice, BaseTotalPrice, CurrencyId, UnitId, CreatedAt, ModifiedAt)
        WHERE UnitPrice IS NOT NULL;
END
GO
