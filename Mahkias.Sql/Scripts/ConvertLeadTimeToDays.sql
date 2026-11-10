-- Lead time is a nullable number of days. Existing text such as "60" or "120 Days" becomes that integer.
-- Non-numeric text becomes NULL. Safe to run more than once.
SET NOCOUNT ON;

DECLARE @tables TABLE (SchemaName SYSNAME, TableName SYSNAME);
INSERT INTO @tables (SchemaName, TableName)
VALUES (N'projects', N'QuotationItems'), (N'projects', N'PurchaseOrderItems');

DECLARE @schema SYSNAME;
DECLARE @table SYSNAME;
DECLARE tables CURSOR LOCAL FAST_FORWARD FOR SELECT SchemaName, TableName FROM @tables;
OPEN tables;
FETCH NEXT FROM tables INTO @schema, @table;
WHILE @@FETCH_STATUS = 0
BEGIN
    IF EXISTS (
        SELECT 1
        FROM INFORMATION_SCHEMA.COLUMNS
        WHERE TABLE_SCHEMA = @schema
          AND TABLE_NAME = @table
          AND COLUMN_NAME = N'LeadTime'
          AND DATA_TYPE IN (N'nvarchar', N'varchar')
    )
    BEGIN
        DECLARE @sql NVARCHAR(MAX) = N'
UPDATE ' + QUOTENAME(@schema) + N'.' + QUOTENAME(@table) + N'
SET LeadTime = CASE
    WHEN LeadTime IS NULL OR LTRIM(RTRIM(LeadTime)) = N'''' THEN NULL
    WHEN LEFT(LTRIM(RTRIM(LeadTime)), PATINDEX(N''%[^0-9]%'', LTRIM(RTRIM(LeadTime)) + N''x'') - 1) <> N''''
     AND LOWER(LTRIM(RTRIM(SUBSTRING(
            LTRIM(RTRIM(LeadTime)),
            PATINDEX(N''%[^0-9]%'', LTRIM(RTRIM(LeadTime)) + N''x''),
            100)))) IN (N'''', N''d'', N''day'', N''days'')
    THEN LEFT(LTRIM(RTRIM(LeadTime)), PATINDEX(N''%[^0-9]%'', LTRIM(RTRIM(LeadTime)) + N''x'') - 1)
    ELSE NULL
END;';
        EXEC sys.sp_executesql @sql;

        DECLARE @alter NVARCHAR(MAX) = N'ALTER TABLE ' + QUOTENAME(@schema) + N'.' + QUOTENAME(@table) + N' ALTER COLUMN LeadTime INT NULL;';
        EXEC sys.sp_executesql @alter;
    END

    FETCH NEXT FROM tables INTO @schema, @table;
END
CLOSE tables;
DEALLOCATE tables;
