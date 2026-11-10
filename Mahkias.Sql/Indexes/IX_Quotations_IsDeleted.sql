CREATE NONCLUSTERED INDEX [IX_Quotations_IsDeleted]
    ON [projects].[Quotations]([Id] ASC)
    INCLUDE([SupplierId], [Code], [CreatedAt])
    WHERE ([IsDeleted]=(0));
