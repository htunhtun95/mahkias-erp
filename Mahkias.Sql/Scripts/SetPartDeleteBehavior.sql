SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

IF EXISTS
(
    SELECT 1
    FROM sys.foreign_keys
    WHERE name = N'FK_Activities_Parts'
      AND delete_referential_action <> 2
)
BEGIN
    ALTER TABLE [projects].[Activities] DROP CONSTRAINT [FK_Activities_Parts];
    ALTER TABLE [projects].[Activities] WITH CHECK
        ADD CONSTRAINT [FK_Activities_Parts] FOREIGN KEY ([PartId]) REFERENCES [projects].[Parts] ([Id]) ON DELETE SET NULL;
END
GO
