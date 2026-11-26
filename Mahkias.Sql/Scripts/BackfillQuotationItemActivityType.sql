-- Copies ActivityTypeId onto quotation lines that are still blank.
-- Only rows with a linked activity and no activity type are updated.
-- Populated activity types are left unchanged. Safe to run more than once.
SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

IF COL_LENGTH(N'projects.QuotationItems', N'ActivityTypeId') IS NULL
    THROW 51000, 'projects.QuotationItems.ActivityTypeId must exist before quotation activity types are backfilled. Run AddQuotationItemActivityType.sql first.', 1;
GO

UPDATE I
SET ActivityTypeId = A.ActivityTypeId
FROM [projects].[QuotationItems] AS I
INNER JOIN [projects].[Activities] AS A ON A.Id = I.ActivityId
WHERE I.ActivityTypeId IS NULL
  AND A.ActivityTypeId IS NOT NULL
  AND EXISTS
  (
      SELECT 1
      FROM [projects].[ActivityTypes] AS T
      WHERE T.Id = A.ActivityTypeId
  );

SELECT UpdatedRows = @@ROWCOUNT;
GO
