-- Repair condition code. Safe to run more than once.
IF OBJECT_ID(N'dbo.Conditions', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM dbo.Conditions WHERE UPPER(Name) = N'RP' OR UPPER(Code) = N'RP')
BEGIN
    INSERT INTO dbo.Conditions (Id, Name, Code)
    VALUES ((SELECT ISNULL(MAX(Id), 0) + 1 FROM dbo.Conditions), N'RP', N'RP');
END
GO
