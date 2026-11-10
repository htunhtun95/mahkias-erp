SET NOCOUNT ON;

IF OBJECT_ID(N'projects.ActivityLabels', N'U') IS NULL
BEGIN
    CREATE TABLE [projects].[ActivityLabels]
    (
        [Id] INT IDENTITY (1, 1) NOT NULL,
        [ProjectId] INT NOT NULL,
        [Name] NVARCHAR(200) NOT NULL,
        [ActivityTypeId] INT NOT NULL,
        [CreatedAt] DATETIME CONSTRAINT [DF_ActivityLabels_CreatedAt] DEFAULT (GETDATE()) NOT NULL,
        CONSTRAINT [PK_ActivityLabels] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_ActivityLabels_Projects] FOREIGN KEY ([ProjectId]) REFERENCES [projects].[Projects] ([Id]),
        CONSTRAINT [FK_ActivityLabels_ActivityTypes] FOREIGN KEY ([ActivityTypeId]) REFERENCES [projects].[ActivityTypes] ([Id]),
        CONSTRAINT [UQ_ActivityLabels_Project_Type_Name] UNIQUE ([ProjectId], [ActivityTypeId], [Name])
    );
END
GO

IF OBJECT_ID(N'projects.ActivityLabelActivities', N'U') IS NULL
BEGIN
    CREATE TABLE [projects].[ActivityLabelActivities]
    (
        [ActivityLabelId] INT NOT NULL,
        [ActivityId] INT NOT NULL,
        CONSTRAINT [PK_ActivityLabelActivities] PRIMARY KEY CLUSTERED ([ActivityLabelId] ASC, [ActivityId] ASC),
        CONSTRAINT [FK_ActivityLabelActivities_ActivityLabels] FOREIGN KEY ([ActivityLabelId]) REFERENCES [projects].[ActivityLabels] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_ActivityLabelActivities_Activities] FOREIGN KEY ([ActivityId]) REFERENCES [projects].[Activities] ([Id]) ON DELETE CASCADE
    );
END
GO

IF OBJECT_ID(N'projects.ActivityLabelGroups', N'U') IS NULL
BEGIN
    CREATE TABLE [projects].[ActivityLabelGroups]
    (
        [ActivityLabelId] INT NOT NULL,
        [ActivityGroupId] INT NOT NULL,
        CONSTRAINT [PK_ActivityLabelGroups] PRIMARY KEY CLUSTERED ([ActivityLabelId] ASC, [ActivityGroupId] ASC),
        CONSTRAINT [FK_ActivityLabelGroups_ActivityLabels] FOREIGN KEY ([ActivityLabelId]) REFERENCES [projects].[ActivityLabels] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_ActivityLabelGroups_ActivityGroups] FOREIGN KEY ([ActivityGroupId]) REFERENCES [projects].[ActivityGroups] ([Id]) ON DELETE CASCADE
    );
END
GO
