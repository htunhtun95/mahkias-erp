CREATE NONCLUSTERED INDEX [IX_Activities_IsDeleted]
    ON [projects].[Activities]([ProjectId] ASC, [Id] ASC)
    INCLUDE([DSNNo], [ActivityTypeId], [ActivityGroupId])
    WHERE ([IsDeleted]=(0));
