SET NOCOUNT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;

-- Add CreatedAt & ModifiedAt if not exists
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[projects].[Projects]') AND name = 'CreatedAt')
BEGIN
    ALTER TABLE [projects].[Projects] ADD
        [CreatedAt] DATETIME NOT NULL CONSTRAINT [DF_Projects_CreatedAt] DEFAULT GETDATE(),
        [ModifiedAt] DATETIME NULL;
END

IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[projects].[Projects]') AND name = 'CreatedAt')
   AND NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[projects].[Projects]') AND name = 'ModifiedAt')
BEGIN
    ALTER TABLE [projects].[Projects] ADD [ModifiedAt] DATETIME NULL;
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[projects].[Activities]') AND name = 'CreatedAt')
BEGIN
    ALTER TABLE [projects].[Activities] ADD
        [CreatedAt] DATETIME NOT NULL CONSTRAINT [DF_Activities_CreatedAt] DEFAULT GETDATE(),
        [ModifiedAt] DATETIME NULL;
END

IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[projects].[Activities]') AND name = 'CreatedAt')
   AND NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[projects].[Activities]') AND name = 'ModifiedAt')
BEGIN
    ALTER TABLE [projects].[Activities] ADD [ModifiedAt] DATETIME NULL;
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[projects].[ActivityAlternativeParts]') AND name = 'CreatedAt')
BEGIN
    ALTER TABLE [projects].[ActivityAlternativeParts] ADD
        [CreatedAt] DATETIME NOT NULL CONSTRAINT [DF_ActivityAlternativeParts_CreatedAt] DEFAULT GETDATE();
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[projects].[ActivityAlternativeParts]') AND name = 'ModifiedAt')
BEGIN
    ALTER TABLE [projects].[ActivityAlternativeParts] ADD [ModifiedAt] DATETIME NULL;
END
GO

-- Populate existing NULL records with Current Date
UPDATE [projects].[Projects] SET [CreatedAt] = GETDATE() WHERE [CreatedAt] IS NULL;
UPDATE [projects].[Activities] SET [CreatedAt] = GETDATE() WHERE [CreatedAt] IS NULL;
UPDATE [projects].[ActivityAlternativeParts] SET [CreatedAt] = GETDATE() WHERE [CreatedAt] IS NULL;
