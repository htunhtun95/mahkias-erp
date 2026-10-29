CREATE NONCLUSTERED INDEX [IX_Suppliers_IsDeleted]
    ON [projects].[Suppliers]([Id] ASC)
    INCLUDE([Name], [Reference])
    WHERE ([IsDeleted]=(0));
