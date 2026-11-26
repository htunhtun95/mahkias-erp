-- Adds quotation line activity type using the existing projects.ActivityTypes lookup.
-- Safe to run more than once. Does not create activity types.

IF COL_LENGTH(N'projects.QuotationItems', N'ActivityTypeId') IS NULL
BEGIN
    ALTER TABLE [projects].[QuotationItems] ADD [ActivityTypeId] INT NULL;

    EXEC(N'
        UPDATE I
        SET ActivityTypeId = A.ActivityTypeId
        FROM [projects].[QuotationItems] AS I
        INNER JOIN [projects].[Activities] AS A ON A.Id = I.ActivityId
        WHERE I.ActivityTypeId IS NULL
          AND A.ActivityTypeId IS NOT NULL;
    ');
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_QuotationItems_ActivityTypes')
    ALTER TABLE [projects].[QuotationItems]
        ADD CONSTRAINT [FK_QuotationItems_ActivityTypes]
        FOREIGN KEY ([ActivityTypeId]) REFERENCES [projects].[ActivityTypes] ([Id]);
GO
