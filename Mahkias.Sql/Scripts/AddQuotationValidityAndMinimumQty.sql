IF COL_LENGTH(N'projects.Quotations', N'QuotedDate') IS NULL
BEGIN
    ALTER TABLE [projects].[Quotations] ADD [QuotedDate] DATETIME NULL;
END

IF COL_LENGTH(N'projects.Quotations', N'ValidUntil') IS NULL
BEGIN
    ALTER TABLE [projects].[Quotations] ADD [ValidUntil] DATETIME NULL;
END

IF COL_LENGTH(N'projects.QuotationItems', N'MinimumQty') IS NULL
BEGIN
    ALTER TABLE [projects].[QuotationItems] ADD [MinimumQty] INT NULL;
END
