CREATE TABLE [projects].[Activities]
(
    [Id] INT IDENTITY (1, 1) NOT NULL,
    [ProjectId] INT NOT NULL,
    [PartNo] NVARCHAR(MAX) NULL,
    [Budget] DECIMAL(18, 2) NULL,
    [Description] NVARCHAR(MAX) NULL,
    [DSNNo] NVARCHAR(100) NULL,
    [Quantity] DECIMAL(18, 2) NULL,
    [ActivityTypeId] INT NULL,
    [ActivityGroupId] INT NULL,
    [CreatedAt] DATETIME CONSTRAINT [DF_Activities_CreatedAt] DEFAULT (GETDATE()) NOT NULL,
    [ModifiedAt] DATETIME NULL,
    [IsDeleted] BIT CONSTRAINT [DF_Activities_IsDeleted] DEFAULT (0) NOT NULL,
    [DeletedAt] DATETIME2 NULL,
    [DeletedBy] NVARCHAR(100) NULL,
    CONSTRAINT [PK_Activities] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_Activities_Projects] FOREIGN KEY ([ProjectId]) REFERENCES [projects].[Projects] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_Activities_ActivityTypes] FOREIGN KEY ([ActivityTypeId]) REFERENCES [projects].[ActivityTypes] ([Id]),
    CONSTRAINT [FK_Activities_ActivityGroups] FOREIGN KEY ([ActivityGroupId]) REFERENCES [projects].[ActivityGroups] ([Id])
);
