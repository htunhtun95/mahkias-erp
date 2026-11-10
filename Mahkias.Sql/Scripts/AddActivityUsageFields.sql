IF COL_LENGTH(N'projects.Activities', N'SerialNo') IS NULL
    ALTER TABLE [projects].[Activities] ADD [SerialNo] NVARCHAR(100) NULL;
GO

IF COL_LENGTH(N'projects.Activities', N'TSN') IS NULL
    ALTER TABLE [projects].[Activities] ADD [TSN] NVARCHAR(100) NULL;
GO

IF COL_LENGTH(N'projects.Activities', N'TSO') IS NULL
    ALTER TABLE [projects].[Activities] ADD [TSO] NVARCHAR(100) NULL;
GO

IF COL_LENGTH(N'projects.Activities', N'CSN') IS NULL
    ALTER TABLE [projects].[Activities] ADD [CSN] NVARCHAR(100) NULL;
GO

IF COL_LENGTH(N'projects.Activities', N'CSO') IS NULL
    ALTER TABLE [projects].[Activities] ADD [CSO] NVARCHAR(100) NULL;
GO
