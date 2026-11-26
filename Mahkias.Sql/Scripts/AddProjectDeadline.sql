IF COL_LENGTH(N'projects.Projects', N'Deadline') IS NULL
BEGIN
    ALTER TABLE projects.Projects ADD Deadline DATETIME2 NULL;
END
GO
