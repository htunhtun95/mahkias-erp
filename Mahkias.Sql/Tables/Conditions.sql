CREATE TABLE [dbo].[Conditions]
(
    [Id] INT NOT NULL,
    [Name] NVARCHAR(100) NOT NULL,
    [Code] NVARCHAR(100) NOT NULL,
    CONSTRAINT [PK_Conditions] PRIMARY KEY CLUSTERED ([Id] ASC)
);
GO

CREATE UNIQUE INDEX [UX_Conditions_Name]
    ON [dbo].[Conditions] ([Name] ASC);
GO

CREATE UNIQUE INDEX [UX_Conditions_Code]
    ON [dbo].[Conditions] ([Code] ASC);
GO
