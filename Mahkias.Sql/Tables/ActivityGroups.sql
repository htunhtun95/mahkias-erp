CREATE TABLE [projects].[ActivityGroups]
(
    [Id] INT IDENTITY (1, 1) NOT NULL,
    [ProjectId] INT NOT NULL,
    [Name] NVARCHAR(200) NOT NULL,
    [Description] NVARCHAR(MAX) NULL,
    [ActivityTypeId] INT NULL,
    [CreatedAt] DATETIME CONSTRAINT [DF_ActivityGroups_CreatedAt] DEFAULT (GETDATE()) NOT NULL,
    CONSTRAINT [PK_ActivityGroups] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_ActivityGroups_Projects] FOREIGN KEY ([ProjectId]) REFERENCES [projects].[Projects] ([Id]),
    CONSTRAINT [FK_ActivityGroups_ActivityTypes] FOREIGN KEY ([ActivityTypeId]) REFERENCES [projects].[ActivityTypes] ([Id])
);
