CREATE UNIQUE NONCLUSTERED INDEX [UX_Suppliers_Reference]
    ON [projects].[Suppliers]([Reference] ASC)
    WHERE ([IsDeleted]=(0) AND [Reference] IS NOT NULL);
