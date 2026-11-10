CREATE NONCLUSTERED INDEX [IX_Projects_IsDeleted]
    ON [projects].[Projects]([Id] ASC)
    INCLUDE([Name], [Reference], [CreatedAt])
    WHERE ([IsDeleted]=(0));
