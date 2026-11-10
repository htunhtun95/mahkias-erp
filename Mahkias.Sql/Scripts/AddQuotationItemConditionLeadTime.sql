IF COL_LENGTH(N'projects.QuotationItems', N'Condition') IS NULL
    ALTER TABLE projects.QuotationItems ADD Condition NVARCHAR(100) NULL;
GO

IF COL_LENGTH(N'projects.QuotationItems', N'LeadTime') IS NULL
    ALTER TABLE projects.QuotationItems ADD LeadTime NVARCHAR(100) NULL;
GO
