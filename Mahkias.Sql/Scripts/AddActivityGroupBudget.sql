IF COL_LENGTH(N'projects.ActivityGroups', N'Budget') IS NULL
BEGIN
    ALTER TABLE [projects].[ActivityGroups] ADD [Budget] DECIMAL(18, 2) NULL;
END
GO

IF COL_LENGTH(N'projects.ActivityGroups', N'ModifiedAt') IS NULL
BEGIN
    ALTER TABLE [projects].[ActivityGroups] ADD [ModifiedAt] DATETIME NULL;
END
GO
