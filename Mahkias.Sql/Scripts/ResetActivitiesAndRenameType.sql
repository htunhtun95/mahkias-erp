-- Clears sample activities and renames the Sourcing activity type to Outright.
-- Activity types live in projects.ActivityTypes (there is no dbo.ActivityTypes lookup).
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;

DELETE FROM [projects].[ActivityAlternativeParts];
DELETE FROM [projects].[Activities];

DBCC CHECKIDENT (N'projects.ActivityAlternativeParts', RESEED, 0);
DBCC CHECKIDENT (N'projects.Activities', RESEED, 0);

UPDATE [projects].[ActivityTypes]
SET [Name] = N'Outright',
    [Slug] = N'outright'
WHERE [Name] = N'Sourcing'
   OR [Slug] = N'sourcing';
