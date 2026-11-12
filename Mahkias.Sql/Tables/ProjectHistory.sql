CREATE TABLE [projects].[ProjectHistory]
(
    [Id] INT IDENTITY (1, 1) NOT NULL,
    [ProjectId] INT NOT NULL,
    [Action] NVARCHAR(40) NOT NULL,
    [Summary] NVARCHAR(400) NOT NULL,
    [ActorName] NVARCHAR(200) NULL,
    [ActorEmail] NVARCHAR(320) NULL,
    -- Reserved for a future users table. No foreign key until that table exists.
    [UserId] INT NULL,
    [CreatedAt] DATETIME CONSTRAINT [DF_ProjectHistory_CreatedAt] DEFAULT (GETDATE()) NOT NULL,
    CONSTRAINT [PK_ProjectHistory] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_ProjectHistory_Projects] FOREIGN KEY ([ProjectId]) REFERENCES [projects].[Projects] ([Id])
);
GO

CREATE INDEX [IX_ProjectHistory_Project]
    ON [projects].[ProjectHistory] ([ProjectId] ASC, [CreatedAt] DESC, [Id] DESC);
GO
