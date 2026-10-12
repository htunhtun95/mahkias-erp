CREATE TABLE [dbo].[Currencies]
(
    [Id] INT IDENTITY (1, 1) NOT NULL,
    [Code] NVARCHAR(3) NOT NULL,
    [Name] NVARCHAR(100) NOT NULL,
    [Symbol] NVARCHAR(10) NOT NULL,
    [IsDefault] BIT CONSTRAINT [DF_Currencies_IsDefault] DEFAULT (0) NOT NULL,
    [IsActive] BIT CONSTRAINT [DF_Currencies_IsActive] DEFAULT (1) NOT NULL,
    CONSTRAINT [PK_Currencies] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [UX_Currencies_Code] UNIQUE ([Code] ASC)
);
GO

CREATE UNIQUE INDEX [UX_Currencies_SingleDefault]
    ON [dbo].[Currencies] ([IsDefault])
    WHERE [IsDefault] = 1;
