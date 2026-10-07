SET NOCOUNT ON;

IF COL_LENGTH(N'projects.Quotations', N'Code') IS NULL
BEGIN
    ALTER TABLE [projects].[Quotations] ADD [Code] NVARCHAR(50) NULL;
END
GO

IF COL_LENGTH(N'projects.Quotations', N'Reference') IS NOT NULL
BEGIN
    EXEC(N'
        UPDATE [projects].[Quotations]
        SET [SupplierReference] = [Reference]
        WHERE ([SupplierReference] IS NULL OR LTRIM(RTRIM([SupplierReference])) = N'''')
          AND [Reference] IS NOT NULL
          AND LTRIM(RTRIM([Reference])) <> N'''';
    ');
END
GO

UPDATE [projects].[Quotations]
SET [Code] = CASE
        WHEN [Id] < 10000 THEN CONCAT(N'Q-', RIGHT(CONCAT(N'0000', CONVERT(NVARCHAR(10), [Id])), 4))
        ELSE CONCAT(N'Q-', CONVERT(NVARCHAR(10), [Id]))
    END
WHERE [Code] IS NULL OR LTRIM(RTRIM([Code])) = N'';
GO

ALTER TABLE [projects].[Quotations] ALTER COLUMN [Code] NVARCHAR(50) NOT NULL;
GO

IF COL_LENGTH(N'projects.Quotations', N'Reference') IS NOT NULL
BEGIN
    ALTER TABLE [projects].[Quotations] DROP COLUMN [Reference];
END
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'UX_Quotations_Code'
      AND object_id = OBJECT_ID(N'projects.Quotations')
)
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX [UX_Quotations_Code]
        ON [projects].[Quotations] ([Code] ASC);
END
GO
