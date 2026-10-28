CREATE TABLE [projects].[ActivityLabelActivities]
(
    [ActivityLabelId] INT NOT NULL,
    [ActivityId] INT NOT NULL,
    CONSTRAINT [PK_ActivityLabelActivities] PRIMARY KEY CLUSTERED ([ActivityLabelId] ASC, [ActivityId] ASC),
    CONSTRAINT [FK_ActivityLabelActivities_ActivityLabels] FOREIGN KEY ([ActivityLabelId]) REFERENCES [projects].[ActivityLabels] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_ActivityLabelActivities_Activities] FOREIGN KEY ([ActivityId]) REFERENCES [projects].[Activities] ([Id]) ON DELETE CASCADE
);
