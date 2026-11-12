-- Project history is the parent audit log. Purchase-order history stays a child of that project.
-- Safe to run more than once.
IF OBJECT_ID(N'projects.ProjectHistory', N'U') IS NULL
BEGIN
    CREATE TABLE projects.ProjectHistory
    (
        Id INT IDENTITY (1, 1) NOT NULL,
        ProjectId INT NOT NULL,
        Action NVARCHAR(40) NOT NULL,
        Summary NVARCHAR(400) NOT NULL,
        ActorName NVARCHAR(200) NULL,
        ActorEmail NVARCHAR(320) NULL,
        UserId INT NULL,
        CreatedAt DATETIME CONSTRAINT DF_ProjectHistory_CreatedAt DEFAULT (GETDATE()) NOT NULL,
        CONSTRAINT PK_ProjectHistory PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_ProjectHistory_Projects FOREIGN KEY (ProjectId) REFERENCES projects.Projects (Id)
    );
END
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_ProjectHistory_Project'
      AND object_id = OBJECT_ID(N'projects.ProjectHistory')
)
BEGIN
    CREATE INDEX IX_ProjectHistory_Project
        ON projects.ProjectHistory (ProjectId, CreatedAt DESC, Id DESC);
END
GO

IF COL_LENGTH(N'projects.PurchaseOrderHistory', N'ProjectId') IS NULL
    ALTER TABLE projects.PurchaseOrderHistory ADD ProjectId INT NULL;
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.foreign_keys
    WHERE name = N'FK_PurchaseOrderHistory_Projects'
)
BEGIN
    ALTER TABLE projects.PurchaseOrderHistory
        ADD CONSTRAINT FK_PurchaseOrderHistory_Projects
        FOREIGN KEY (ProjectId) REFERENCES projects.Projects (Id);
END
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_PurchaseOrderHistory_Project'
      AND object_id = OBJECT_ID(N'projects.PurchaseOrderHistory')
)
BEGIN
    CREATE INDEX IX_PurchaseOrderHistory_Project
        ON projects.PurchaseOrderHistory (ProjectId, CreatedAt, Id)
        WHERE ProjectId IS NOT NULL;
END
GO

UPDATE H
SET ProjectId = OnlyProject.ProjectId
FROM projects.PurchaseOrderHistory AS H
INNER JOIN (
    SELECT PurchaseOrderId, ProjectId = MIN(ProjectId)
    FROM projects.PurchaseOrderProjects
    GROUP BY PurchaseOrderId
    HAVING COUNT(*) = 1
) AS OnlyProject ON OnlyProject.PurchaseOrderId = H.PurchaseOrderId
WHERE H.ProjectId IS NULL;
GO

INSERT INTO projects.ProjectHistory (ProjectId, Action, Summary, CreatedAt)
SELECT
    P.Id,
    N'Created',
    LEFT(N'Created ' + P.Name, 400),
    P.CreatedAt
FROM projects.Projects AS P
WHERE NOT EXISTS (
    SELECT 1
    FROM projects.ProjectHistory AS H
    WHERE H.ProjectId = P.Id
);
GO
