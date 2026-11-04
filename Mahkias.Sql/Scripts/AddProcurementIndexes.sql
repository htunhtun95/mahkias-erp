IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_QuotationItems_QuotationId'
      AND object_id = OBJECT_ID(N'projects.QuotationItems')
)
    CREATE INDEX [IX_QuotationItems_QuotationId]
        ON [projects].[QuotationItems] ([QuotationId])
        INCLUDE ([ActivityId], [UnitPrice], [TotalPrice], [Quantity], [AwardedQuantity]);

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_QuotationProjects_ProjectId'
      AND object_id = OBJECT_ID(N'projects.QuotationProjects')
)
    CREATE INDEX [IX_QuotationProjects_ProjectId]
        ON [projects].[QuotationProjects] ([ProjectId], [QuotationId]);
