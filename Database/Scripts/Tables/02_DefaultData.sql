-- Initial lookup data. Existing rows are left unchanged.

IF NOT EXISTS (SELECT 1 FROM [projects].[ActivityTypes])
BEGIN
    INSERT INTO [projects].[ActivityTypes] ([Id], [Name], [Slug])
    VALUES
        (1, N'Overhaul', N'overhaul'),
        (2, N'Exchange', N'exchange'),
        (3, N'Outright', N'outright');
END
GO

IF NOT EXISTS (SELECT 1 FROM [dbo].[Currencies])
BEGIN
    INSERT INTO [dbo].[Currencies] ([Code], [Name], [Symbol], [IsDefault], [IsActive])
    VALUES
        (N'USD', N'US Dollar', N'$', 1, 1),
        (N'EUR', N'Euro', NCHAR(8364), 0, 1),
        (N'GBP', N'Pound Sterling', NCHAR(163), 0, 1),
        (N'THB', N'Thai Baht', NCHAR(3647), 0, 1),
        (N'MYR', N'Malaysian Ringgit', N'RM', 0, 1);
END
GO
