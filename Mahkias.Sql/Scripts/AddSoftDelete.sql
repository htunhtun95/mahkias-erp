IF COL_LENGTH(N'projects.Projects', N'IsDeleted') IS NULL
    ALTER TABLE projects.Projects ADD IsDeleted BIT NOT NULL CONSTRAINT DF_Projects_IsDeleted DEFAULT (0);
GO
IF COL_LENGTH(N'projects.Projects', N'DeletedAt') IS NULL
    ALTER TABLE projects.Projects ADD DeletedAt DATETIME2 NULL;
GO
IF COL_LENGTH(N'projects.Projects', N'DeletedBy') IS NULL
    ALTER TABLE projects.Projects ADD DeletedBy NVARCHAR(100) NULL;
GO

IF COL_LENGTH(N'projects.Activities', N'IsDeleted') IS NULL
    ALTER TABLE projects.Activities ADD IsDeleted BIT NOT NULL CONSTRAINT DF_Activities_IsDeleted DEFAULT (0);
GO
IF COL_LENGTH(N'projects.Activities', N'DeletedAt') IS NULL
    ALTER TABLE projects.Activities ADD DeletedAt DATETIME2 NULL;
GO
IF COL_LENGTH(N'projects.Activities', N'DeletedBy') IS NULL
    ALTER TABLE projects.Activities ADD DeletedBy NVARCHAR(100) NULL;
GO

IF COL_LENGTH(N'projects.Quotations', N'IsDeleted') IS NULL
    ALTER TABLE projects.Quotations ADD IsDeleted BIT NOT NULL CONSTRAINT DF_Quotations_IsDeleted DEFAULT (0);
GO
IF COL_LENGTH(N'projects.Quotations', N'DeletedAt') IS NULL
    ALTER TABLE projects.Quotations ADD DeletedAt DATETIME2 NULL;
GO
IF COL_LENGTH(N'projects.Quotations', N'DeletedBy') IS NULL
    ALTER TABLE projects.Quotations ADD DeletedBy NVARCHAR(100) NULL;
GO

IF COL_LENGTH(N'projects.Suppliers', N'IsDeleted') IS NULL
    ALTER TABLE projects.Suppliers ADD IsDeleted BIT NOT NULL CONSTRAINT DF_Suppliers_IsDeleted DEFAULT (0);
GO
IF COL_LENGTH(N'projects.Suppliers', N'DeletedAt') IS NULL
    ALTER TABLE projects.Suppliers ADD DeletedAt DATETIME2 NULL;
GO
IF COL_LENGTH(N'projects.Suppliers', N'DeletedBy') IS NULL
    ALTER TABLE projects.Suppliers ADD DeletedBy NVARCHAR(100) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Projects_IsDeleted' AND object_id = OBJECT_ID(N'projects.Projects'))
    CREATE NONCLUSTERED INDEX IX_Projects_IsDeleted
        ON projects.Projects (Id)
        INCLUDE (Name, Reference, CreatedAt)
        WHERE IsDeleted = 0;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Activities_IsDeleted' AND object_id = OBJECT_ID(N'projects.Activities'))
    CREATE NONCLUSTERED INDEX IX_Activities_IsDeleted
        ON projects.Activities (ProjectId, Id)
        INCLUDE (DSNNo, ActivityTypeId, ActivityGroupId)
        WHERE IsDeleted = 0;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Quotations_IsDeleted' AND object_id = OBJECT_ID(N'projects.Quotations'))
    CREATE NONCLUSTERED INDEX IX_Quotations_IsDeleted
        ON projects.Quotations (Id)
        INCLUDE (SupplierId, Code, CreatedAt)
        WHERE IsDeleted = 0;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Suppliers_IsDeleted' AND object_id = OBJECT_ID(N'projects.Suppliers'))
    CREATE NONCLUSTERED INDEX IX_Suppliers_IsDeleted
        ON projects.Suppliers (Id)
        INCLUDE (Name, Reference)
        WHERE IsDeleted = 0;
GO
