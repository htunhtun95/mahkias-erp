CREATE TABLE [dbo].[Units]
(
    [Id] INT NOT NULL,
    [Name] NVARCHAR(100) NOT NULL,
    [Code] NVARCHAR(100) NOT NULL,
    [IsDefault] BIT CONSTRAINT [DF_Units_IsDefault] DEFAULT (0) NOT NULL,
    CONSTRAINT [PK_Units] PRIMARY KEY CLUSTERED ([Id] ASC)
);
GO

CREATE UNIQUE INDEX [UX_Units_Name]
    ON [dbo].[Units] ([Name] ASC);
GO

CREATE UNIQUE INDEX [UX_Units_Code]
    ON [dbo].[Units] ([Code] ASC);
GO

CREATE UNIQUE INDEX [UX_Units_SingleDefault]
    ON [dbo].[Units] ([IsDefault])
    WHERE [IsDefault] = 1;
GO
