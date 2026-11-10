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
