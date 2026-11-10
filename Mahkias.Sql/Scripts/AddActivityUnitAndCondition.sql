IF COL_LENGTH(N'projects.Activities', N'Unit') IS NULL
    ALTER TABLE [projects].[Activities] ADD [Unit] NVARCHAR(100) NULL;
GO

IF COL_LENGTH(N'projects.Activities', N'Condition') IS NULL
    ALTER TABLE [projects].[Activities] ADD [Condition] NVARCHAR(100) NULL;
GO
