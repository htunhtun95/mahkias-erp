IF OBJECT_ID(N'dbo.Units', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Units
    (
        Id INT NOT NULL,
        Name NVARCHAR(100) NOT NULL,
        Code NVARCHAR(100) NOT NULL,
        IsDefault BIT CONSTRAINT DF_Units_IsDefault DEFAULT (0) NOT NULL,
        CONSTRAINT PK_Units PRIMARY KEY CLUSTERED (Id ASC)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_Units_Name' AND object_id = OBJECT_ID(N'dbo.Units'))
    CREATE UNIQUE INDEX UX_Units_Name ON dbo.Units (Name);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_Units_Code' AND object_id = OBJECT_ID(N'dbo.Units'))
    CREATE UNIQUE INDEX UX_Units_Code ON dbo.Units (Code);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_Units_SingleDefault' AND object_id = OBJECT_ID(N'dbo.Units'))
    CREATE UNIQUE INDEX UX_Units_SingleDefault ON dbo.Units (IsDefault) WHERE IsDefault = 1;

IF OBJECT_ID(N'dbo.Conditions', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Conditions
    (
        Id INT NOT NULL,
        Name NVARCHAR(100) NOT NULL,
        Code NVARCHAR(100) NOT NULL,
        CONSTRAINT PK_Conditions PRIMARY KEY CLUSTERED (Id ASC)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_Conditions_Name' AND object_id = OBJECT_ID(N'dbo.Conditions'))
    CREATE UNIQUE INDEX UX_Conditions_Name ON dbo.Conditions (Name);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_Conditions_Code' AND object_id = OBJECT_ID(N'dbo.Conditions'))
    CREATE UNIQUE INDEX UX_Conditions_Code ON dbo.Conditions (Code);
GO

IF NOT EXISTS (SELECT 1 FROM dbo.Units WHERE UPPER(Code) = N'EA')
    INSERT INTO dbo.Units (Id, Name, Code, IsDefault)
    VALUES ((SELECT ISNULL(MAX(Id), 0) + 1 FROM dbo.Units), N'EA', N'EA', 1);

IF NOT EXISTS (SELECT 1 FROM dbo.Units WHERE UPPER(Code) = N'TUBE')
    INSERT INTO dbo.Units (Id, Name, Code, IsDefault)
    VALUES ((SELECT ISNULL(MAX(Id), 0) + 1 FROM dbo.Units), N'TUBE', N'TUBE', 0);

IF NOT EXISTS (SELECT 1 FROM dbo.Units WHERE UPPER(Code) = N'ROLL')
    INSERT INTO dbo.Units (Id, Name, Code, IsDefault)
    VALUES ((SELECT ISNULL(MAX(Id), 0) + 1 FROM dbo.Units), N'ROLL', N'ROLL', 0);

IF NOT EXISTS (SELECT 1 FROM dbo.Units WHERE UPPER(Code) = N'CAN')
    INSERT INTO dbo.Units (Id, Name, Code, IsDefault)
    VALUES ((SELECT ISNULL(MAX(Id), 0) + 1 FROM dbo.Units), N'CAN', N'CAN', 0);

IF NOT EXISTS (SELECT 1 FROM dbo.Units WHERE IsDefault = 1)
    UPDATE dbo.Units SET IsDefault = 1 WHERE UPPER(Code) = N'EA';

IF NOT EXISTS (SELECT 1 FROM dbo.Conditions WHERE UPPER(Name) = N'NE')
    INSERT INTO dbo.Conditions (Id, Name, Code) VALUES ((SELECT ISNULL(MAX(Id), 0) + 1 FROM dbo.Conditions), N'NE', N'NE');
IF NOT EXISTS (SELECT 1 FROM dbo.Conditions WHERE UPPER(Name) = N'NS')
    INSERT INTO dbo.Conditions (Id, Name, Code) VALUES ((SELECT ISNULL(MAX(Id), 0) + 1 FROM dbo.Conditions), N'NS', N'NS');
IF NOT EXISTS (SELECT 1 FROM dbo.Conditions WHERE UPPER(Name) = N'OH')
    INSERT INTO dbo.Conditions (Id, Name, Code) VALUES ((SELECT ISNULL(MAX(Id), 0) + 1 FROM dbo.Conditions), N'OH', N'OH');
IF NOT EXISTS (SELECT 1 FROM dbo.Conditions WHERE UPPER(Name) = N'SV')
    INSERT INTO dbo.Conditions (Id, Name, Code) VALUES ((SELECT ISNULL(MAX(Id), 0) + 1 FROM dbo.Conditions), N'SV', N'SV');
IF NOT EXISTS (SELECT 1 FROM dbo.Conditions WHERE UPPER(Name) = N'FN')
    INSERT INTO dbo.Conditions (Id, Name, Code) VALUES ((SELECT ISNULL(MAX(Id), 0) + 1 FROM dbo.Conditions), N'FN', N'FN');
IF NOT EXISTS (SELECT 1 FROM dbo.Conditions WHERE UPPER(Name) = N'AR')
    INSERT INTO dbo.Conditions (Id, Name, Code) VALUES ((SELECT ISNULL(MAX(Id), 0) + 1 FROM dbo.Conditions), N'AR', N'AR');
IF NOT EXISTS (SELECT 1 FROM dbo.Conditions WHERE UPPER(Name) = N'EX')
    INSERT INTO dbo.Conditions (Id, Name, Code) VALUES ((SELECT ISNULL(MAX(Id), 0) + 1 FROM dbo.Conditions), N'EX', N'EX');
IF NOT EXISTS (SELECT 1 FROM dbo.Conditions WHERE UPPER(Name) = N'EO')
    INSERT INTO dbo.Conditions (Id, Name, Code) VALUES ((SELECT ISNULL(MAX(Id), 0) + 1 FROM dbo.Conditions), N'EO', N'EO');

IF COL_LENGTH(N'projects.QuotationItems', N'UnitId') IS NULL
    ALTER TABLE projects.QuotationItems ADD UnitId INT NULL;

IF COL_LENGTH(N'projects.QuotationItems', N'ConditionId') IS NULL
    ALTER TABLE projects.QuotationItems ADD ConditionId INT NULL;

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_QuotationItems_Units')
    ALTER TABLE projects.QuotationItems ADD CONSTRAINT FK_QuotationItems_Units FOREIGN KEY (UnitId) REFERENCES dbo.Units (Id);

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_QuotationItems_Conditions')
    ALTER TABLE projects.QuotationItems ADD CONSTRAINT FK_QuotationItems_Conditions FOREIGN KEY (ConditionId) REFERENCES dbo.Conditions (Id);
GO

;WITH Existing AS (
    SELECT DISTINCT Name = LEFT(LTRIM(RTRIM(Condition)), 100)
    FROM projects.QuotationItems
    WHERE NULLIF(LTRIM(RTRIM(Condition)), N'') IS NOT NULL
),
Missing AS (
    SELECT
        Existing.Name,
        Code = LEFT(REPLACE(Existing.Name, N' ', N''), 100)
    FROM Existing
    WHERE LEFT(REPLACE(Existing.Name, N' ', N''), 100) <> N''
      AND NOT EXISTS (SELECT 1 FROM dbo.Conditions C WHERE UPPER(C.Name) = UPPER(Existing.Name))
      AND NOT EXISTS (
          SELECT 1 FROM dbo.Conditions C
          WHERE UPPER(C.Code) = UPPER(LEFT(REPLACE(Existing.Name, N' ', N''), 100))
      )
)
INSERT INTO dbo.Conditions (Id, Name, Code)
SELECT (SELECT ISNULL(MAX(Id), 0) FROM dbo.Conditions) + ROW_NUMBER() OVER (ORDER BY Missing.Name),
       Missing.Name,
       Missing.Code
FROM Missing;

UPDATE I
SET ConditionId = C.Id
FROM projects.QuotationItems I
INNER JOIN dbo.Conditions C
    ON UPPER(C.Name) = UPPER(LEFT(LTRIM(RTRIM(I.Condition)), 100))
WHERE I.ConditionId IS NULL
  AND NULLIF(LTRIM(RTRIM(I.Condition)), N'') IS NOT NULL;

UPDATE I
SET ConditionId = C.Id
FROM projects.QuotationItems I
INNER JOIN dbo.Conditions C
    ON UPPER(C.Code) = UPPER(LEFT(REPLACE(LTRIM(RTRIM(I.Condition)), N' ', N''), 100))
WHERE I.ConditionId IS NULL
  AND NULLIF(LTRIM(RTRIM(I.Condition)), N'') IS NOT NULL;
