-- Seeds part-level alternate links from combined part numbers.
-- AddParts.sql already runs this seed. Run this script again after
-- SplitCombinedOrParts.sql, and any time the links need to be filled in.
-- Re-running does not insert a duplicate link or a part as its own alternate.
-- ActivityAlternativeParts is not read or written.
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

IF OBJECT_ID(N'projects.Parts', N'U') IS NULL
    THROW 51000, 'projects.Parts must exist before alternate parts are seeded. Run AddParts.sql first.', 1;
GO

IF OBJECT_ID(N'projects.PartAlternates', N'U') IS NULL
BEGIN
    CREATE TABLE [projects].[PartAlternates]
    (
        [Id] INT IDENTITY (1, 1) NOT NULL,
        [PartId] INT NOT NULL,
        [AlternatePartId] INT NOT NULL,
        [CreatedAt] DATETIME2 CONSTRAINT [DF_PartAlternates_CreatedAt] DEFAULT (SYSUTCDATETIME()) NOT NULL,
        CONSTRAINT [PK_PartAlternates] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_PartAlternates_Parts] FOREIGN KEY ([PartId]) REFERENCES [projects].[Parts] ([Id]),
        CONSTRAINT [FK_PartAlternates_AlternateParts] FOREIGN KEY ([AlternatePartId]) REFERENCES [projects].[Parts] ([Id]),
        CONSTRAINT [CK_PartAlternates_NotSelf] CHECK ([PartId] <> [AlternatePartId])
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_PartAlternates_Part_Alternate' AND object_id = OBJECT_ID(N'projects.PartAlternates'))
BEGIN
    CREATE UNIQUE INDEX [UX_PartAlternates_Part_Alternate]
        ON [projects].[PartAlternates]([PartId] ASC, [AlternatePartId] ASC);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_PartAlternates_AlternatePartId' AND object_id = OBJECT_ID(N'projects.PartAlternates'))
BEGIN
    CREATE INDEX [IX_PartAlternates_AlternatePartId]
        ON [projects].[PartAlternates]([AlternatePartId] ASC);
END
GO

CREATE OR ALTER PROCEDURE [dbo].[SeedPartAlternates]
(
    @InsertedLinks INT = NULL OUTPUT
)
AS
BEGIN
    SET NOCOUNT ON;

    IF OBJECT_ID(N'projects.PartAlternates', N'U') IS NULL
        THROW 51000, 'projects.PartAlternates must exist before alternate parts are seeded.', 1;

    IF OBJECT_ID(N'dbo.SplitAlternativePartNumbers', N'TF') IS NULL
        THROW 51000, 'dbo.SplitAlternativePartNumbers must exist before alternate parts are seeded.', 1;

    INSERT INTO projects.PartAlternates (PartId, AlternatePartId)
    SELECT Pair.MainPartId, Pair.AlternatePartId
    FROM
    (
        SELECT DISTINCT
            MainPart.Id AS MainPartId,
            AltPart.Id AS AlternatePartId
        FROM
        (
            SELECT LTRIM(RTRIM(A.PartNo)) AS PartNo
            FROM projects.Activities AS A
            WHERE NULLIF(LTRIM(RTRIM(A.PartNo)), N'') IS NOT NULL
            UNION
            SELECT LTRIM(RTRIM(I.PartNo))
            FROM projects.QuotationItems AS I
            WHERE NULLIF(LTRIM(RTRIM(I.PartNo)), N'') IS NOT NULL
            UNION
            SELECT LTRIM(RTRIM(I.PartNo))
            FROM projects.PurchaseOrderItems AS I
            WHERE NULLIF(LTRIM(RTRIM(I.PartNo)), N'') IS NOT NULL
            UNION
            SELECT LTRIM(RTRIM(P.PartNo))
            FROM projects.Parts AS P
            WHERE NULLIF(LTRIM(RTRIM(P.PartNo)), N'') IS NOT NULL
        ) AS Source
        CROSS APPLY dbo.SplitAlternativePartNumbers(Source.PartNo) AS MainPiece
        CROSS APPLY dbo.SplitAlternativePartNumbers(Source.PartNo) AS AltPiece
        CROSS APPLY
        (
            SELECT TOP (1) P.Id
            FROM projects.Parts AS P
            WHERE HASHBYTES('SHA2_256', LOWER(LTRIM(RTRIM(CAST(P.PartNo AS NVARCHAR(4000))))))
                = HASHBYTES('SHA2_256', LOWER(LTRIM(RTRIM(CAST(MainPiece.PartValue AS NVARCHAR(4000))))))
              AND (SELECT COUNT(*) FROM dbo.SplitAlternativePartNumbers(P.PartNo)) = 1
            ORDER BY P.Id
        ) AS MainPart
        CROSS APPLY
        (
            SELECT TOP (1) P.Id
            FROM projects.Parts AS P
            WHERE HASHBYTES('SHA2_256', LOWER(LTRIM(RTRIM(CAST(P.PartNo AS NVARCHAR(4000))))))
                = HASHBYTES('SHA2_256', LOWER(LTRIM(RTRIM(CAST(AltPiece.PartValue AS NVARCHAR(4000))))))
              AND (SELECT COUNT(*) FROM dbo.SplitAlternativePartNumbers(P.PartNo)) = 1
            ORDER BY P.Id
        ) AS AltPart
        WHERE MainPiece.IsMain = 1
          AND AltPiece.IsMain = 0
          AND MainPart.Id <> AltPart.Id
    ) AS Pair
    WHERE NOT EXISTS
    (
        SELECT 1
        FROM projects.PartAlternates AS Existing
        WHERE Existing.PartId = Pair.MainPartId
          AND Existing.AlternatePartId = Pair.AlternatePartId
    );

    SET @InsertedLinks = @@ROWCOUNT;
END
GO

SET NOCOUNT ON;

DECLARE @ProjectAltsBefore INT = (SELECT COUNT(*) FROM projects.ActivityAlternativeParts);
DECLARE @InsertedLinks INT = 0;

EXEC dbo.SeedPartAlternates @InsertedLinks = @InsertedLinks OUTPUT;

SELECT
    @InsertedLinks AS InsertedLinks,
    (SELECT COUNT(*) FROM projects.PartAlternates) AS PartAlternates,
    (SELECT COUNT(*) FROM projects.PartAlternates WHERE PartId = AlternatePartId) AS SelfLinks,
    (
        SELECT COUNT(*)
        FROM
        (
            SELECT PartId, AlternatePartId
            FROM projects.PartAlternates
            GROUP BY PartId, AlternatePartId
            HAVING COUNT(*) > 1
        ) AS D
    ) AS DuplicateLinks,
    CASE
        WHEN (SELECT COUNT(*) FROM projects.ActivityAlternativeParts) = @ProjectAltsBefore THEN 1
        ELSE 0
    END AS ProjectAlternatesUnchanged;
GO
