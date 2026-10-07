IF COL_LENGTH(N'projects.ActivityGroups', N'ActivityTypeId') IS NULL
BEGIN
    ALTER TABLE [projects].[ActivityGroups] ADD [ActivityTypeId] INT NULL;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_ActivityGroups_ActivityTypes')
BEGIN
    ALTER TABLE [projects].[ActivityGroups]
        ADD CONSTRAINT [FK_ActivityGroups_ActivityTypes]
            FOREIGN KEY ([ActivityTypeId]) REFERENCES [projects].[ActivityTypes] ([Id]);
END
GO

UPDATE G
SET ActivityTypeId = Locked.TypeId
FROM [projects].[ActivityGroups] AS G
CROSS APPLY (
    SELECT
        TypeId = MIN(A.ActivityTypeId),
        TypeCount = COUNT(DISTINCT A.ActivityTypeId),
        Missing = SUM(CASE WHEN A.ActivityTypeId IS NULL THEN 1 ELSE 0 END)
    FROM [projects].[Activities] AS A
    WHERE A.ActivityGroupId = G.Id
) AS Locked
WHERE G.ActivityTypeId IS NULL
  AND Locked.TypeCount = 1
  AND Locked.Missing = 0
  AND Locked.TypeId IS NOT NULL;
