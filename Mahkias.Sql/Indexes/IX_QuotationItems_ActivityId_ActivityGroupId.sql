CREATE NONCLUSTERED INDEX [IX_QuotationItems_ActivityId_ActivityGroupId]
    ON [projects].[QuotationItems] ([ActivityId] ASC, [ActivityGroupId] ASC);
