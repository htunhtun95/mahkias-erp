-- Filtered indexes on projects, activities, quotations, and suppliers require
-- QUOTED_IDENTIFIER ON for any update. The setting is stored with each procedure
-- at creation time, so this script turns it on and reports any procedure still off.
-- Safe to run more than once. It does not change procedure text.
SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

SELECT
    SchemaName = SCHEMA_NAME(p.schema_id),
    ProcedureName = p.name,
    QuotedIdentifierOn = OBJECTPROPERTY(p.object_id, 'ExecIsQuotedIdentOn')
FROM sys.procedures AS p
WHERE p.is_ms_shipped = 0
  AND OBJECTPROPERTY(p.object_id, 'ExecIsQuotedIdentOn') = 0
ORDER BY p.name;
GO
