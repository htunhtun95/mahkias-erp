CREATE TABLE [projects].[ActivityAlternativeParts]
(
    [Id] INT IDENTITY (1, 1) NOT NULL,
    [ActivityId] INT NOT NULL,
    [ProjectId] INT NOT NULL,
    [MainPartNo] NVARCHAR(100) NOT NULL,
    [AlternativePartNo] NVARCHAR(100) NOT NULL,
    [SortOrder] INT CONSTRAINT [DF_ActivityAlternativeParts_SortOrder] DEFAULT (1) NOT NULL,
    [CreatedAt] DATETIME CONSTRAINT [DF_ActivityAlternativeParts_CreatedAt] DEFAULT (GETDATE()) NOT NULL,
    [ModifiedAt] DATETIME NULL,
    CONSTRAINT [PK_ActivityAlternativeParts] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_ActivityAlternativeParts_Activities] FOREIGN KEY ([ActivityId]) REFERENCES [projects].[Activities] ([Id]) ON DELETE CASCADE
);
