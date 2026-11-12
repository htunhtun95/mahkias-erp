-- Quotation lines keep their own copy of the activity technical fields.
-- Safe to run more than once. Existing lines with no values yet take the linked activity's current values.
IF COL_LENGTH(N'projects.QuotationItems', N'SerialNo') IS NULL
    ALTER TABLE [projects].[QuotationItems] ADD [SerialNo] NVARCHAR(100) NULL;
GO

IF COL_LENGTH(N'projects.QuotationItems', N'TSN') IS NULL
    ALTER TABLE [projects].[QuotationItems] ADD [TSN] NVARCHAR(100) NULL;
GO

IF COL_LENGTH(N'projects.QuotationItems', N'TSO') IS NULL
    ALTER TABLE [projects].[QuotationItems] ADD [TSO] NVARCHAR(100) NULL;
GO

IF COL_LENGTH(N'projects.QuotationItems', N'CSN') IS NULL
    ALTER TABLE [projects].[QuotationItems] ADD [CSN] NVARCHAR(100) NULL;
GO

IF COL_LENGTH(N'projects.QuotationItems', N'CSO') IS NULL
    ALTER TABLE [projects].[QuotationItems] ADD [CSO] NVARCHAR(100) NULL;
GO

UPDATE I
SET
    SerialNo = A.SerialNo,
    TSN = A.TSN,
    TSO = A.TSO,
    CSN = A.CSN,
    CSO = A.CSO
FROM [projects].[QuotationItems] AS I
INNER JOIN [projects].[Activities] AS A ON A.Id = I.ActivityId
WHERE I.SerialNo IS NULL
  AND I.TSN IS NULL
  AND I.TSO IS NULL
  AND I.CSN IS NULL
  AND I.CSO IS NULL
  AND (
        A.SerialNo IS NOT NULL
     OR A.TSN IS NOT NULL
     OR A.TSO IS NOT NULL
     OR A.CSN IS NOT NULL
     OR A.CSO IS NOT NULL
  );
GO
