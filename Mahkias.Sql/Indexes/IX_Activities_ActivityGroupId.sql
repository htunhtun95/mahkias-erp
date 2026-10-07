CREATE NONCLUSTERED INDEX [IX_Activities_ActivityGroupId]
    ON [projects].[Activities] ([ActivityGroupId] ASC)
    WHERE [ActivityGroupId] IS NOT NULL;
