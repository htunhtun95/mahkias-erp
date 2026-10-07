-- Removes sample projects and their dependent rows, then reseeds identity values to 0.
-- Activity types stay. The lookup table is projects.ActivityTypes; dbo.ActivityTypes is updated only when it exists.
SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

USE [MahkiasERP];
GO

IF OBJECT_ID(N'projects.ActivityAlternativeParts', N'U') IS NOT NULL
    DELETE FROM [projects].[ActivityAlternativeParts];

IF OBJECT_ID(N'projects.PartSourcingItems', N'U') IS NOT NULL
    DELETE FROM [projects].[PartSourcingItems];

IF OBJECT_ID(N'projects.PartSourcing', N'U') IS NOT NULL
    DELETE FROM [projects].[PartSourcing];

IF OBJECT_ID(N'projects.ProjectLinks', N'U') IS NOT NULL
    DELETE FROM [projects].[ProjectLinks];

IF OBJECT_ID(N'projects.Activities', N'U') IS NOT NULL
    DELETE FROM [projects].[Activities];

DELETE FROM [projects].[Projects];

IF OBJECT_ID(N'projects.ActivityAlternativeParts', N'U') IS NOT NULL
    DBCC CHECKIDENT (N'projects.ActivityAlternativeParts', RESEED, 0);

IF OBJECT_ID(N'projects.PartSourcingItems', N'U') IS NOT NULL
    DBCC CHECKIDENT (N'projects.PartSourcingItems', RESEED, 0);

IF OBJECT_ID(N'projects.PartSourcing', N'U') IS NOT NULL
    DBCC CHECKIDENT (N'projects.PartSourcing', RESEED, 0);

IF OBJECT_ID(N'projects.Activities', N'U') IS NOT NULL
    DBCC CHECKIDENT (N'projects.Activities', RESEED, 0);

DBCC CHECKIDENT (N'projects.Projects', RESEED, 0);

IF OBJECT_ID(N'dbo.ActivityTypes', N'U') IS NOT NULL
BEGIN
    EXEC(N'UPDATE [dbo].[ActivityTypes] SET [Name] = N''Outright'' WHERE [Name] = N''Sourcing''');
END

IF OBJECT_ID(N'projects.ActivityTypes', N'U') IS NOT NULL
   AND EXISTS (SELECT 1 FROM [projects].[ActivityTypes] WHERE [Name] = N'Sourcing')
BEGIN
    UPDATE [projects].[ActivityTypes]
    SET [Name] = N'Outright'
    WHERE [Name] = N'Sourcing';
END
GO

USE [Mahkias];
GO

DELETE FROM [projects].[ActivityAlternativeParts];
DELETE FROM [projects].[Activities];
DELETE FROM [projects].[Projects];

DBCC CHECKIDENT (N'projects.ActivityAlternativeParts', RESEED, 0);
DBCC CHECKIDENT (N'projects.Activities', RESEED, 0);
DBCC CHECKIDENT (N'projects.Projects', RESEED, 0);

IF EXISTS (SELECT 1 FROM [projects].[ActivityTypes] WHERE [Name] = N'Sourcing' OR [Slug] = N'sourcing')
BEGIN
    UPDATE [projects].[ActivityTypes]
    SET [Name] = N'Outright',
        [Slug] = N'outright'
    WHERE [Name] = N'Sourcing'
       OR [Slug] = N'sourcing';
END
GO
