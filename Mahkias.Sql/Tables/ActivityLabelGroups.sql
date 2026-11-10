CREATE TABLE [projects].[ActivityLabelGroups]
(
    [ActivityLabelId] INT NOT NULL,
    [ActivityGroupId] INT NOT NULL,
    CONSTRAINT [PK_ActivityLabelGroups] PRIMARY KEY CLUSTERED ([ActivityLabelId] ASC, [ActivityGroupId] ASC),
    CONSTRAINT [FK_ActivityLabelGroups_ActivityLabels] FOREIGN KEY ([ActivityLabelId]) REFERENCES [projects].[ActivityLabels] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_ActivityLabelGroups_ActivityGroups] FOREIGN KEY ([ActivityGroupId]) REFERENCES [projects].[ActivityGroups] ([Id]) ON DELETE CASCADE
);
