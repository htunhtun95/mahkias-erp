-- Initial Part / PartDescription backfill.
-- Run CheckPartBackfill.sql first. Then run this script, SetPartDeleteBehavior.sql,
-- and SeedPartAlternates.sql. Run CheckPartBackfill.sql again afterwards.
-- If CombinedPartsRemaining is greater than 0, run SplitCombinedOrParts.sql,
-- run SeedPartAlternates.sql again, and check once more.
--
-- Pieces follow dbo.SplitAlternativePartNumbers: or, (or), and a slash between
-- two full part numbers. Activities link to the main piece. Quotation and
-- purchase-order lines keep their saved part number; their descriptions are
-- attached to the main piece, or to the alternate piece when the line names it.
-- Re-running does not insert a second Part or PartDescription for the same value.
-- This script also creates projects.PartAlternates and links the main piece to
-- each later piece. SeedPartAlternates.sql repeats that link step safely.
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

IF OBJECT_ID(N'projects.Parts', N'U') IS NULL
BEGIN
    CREATE TABLE [projects].[Parts]
    (
        [Id] INT IDENTITY (1, 1) NOT NULL,
        [PartNo] NVARCHAR(MAX) NOT NULL,
        [CreatedAt] DATETIME2 CONSTRAINT [DF_Parts_CreatedAt] DEFAULT (SYSUTCDATETIME()) NOT NULL,
        CONSTRAINT [PK_Parts] PRIMARY KEY CLUSTERED ([Id] ASC)
    );
END
GO

IF OBJECT_ID(N'projects.PartDescriptions', N'U') IS NULL
BEGIN
    CREATE TABLE [projects].[PartDescriptions]
    (
        [Id] INT IDENTITY (1, 1) NOT NULL,
        [PartId] INT NOT NULL,
        [Description] NVARCHAR(MAX) NOT NULL,
        [CreatedAt] DATETIME2 CONSTRAINT [DF_PartDescriptions_CreatedAt] DEFAULT (SYSUTCDATETIME()) NOT NULL,
        CONSTRAINT [PK_PartDescriptions] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_PartDescriptions_Parts] FOREIGN KEY ([PartId]) REFERENCES [projects].[Parts] ([Id])
    );
END
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_PartDescriptions_PartId'
      AND object_id = OBJECT_ID(N'projects.PartDescriptions')
)
BEGIN
    CREATE INDEX [IX_PartDescriptions_PartId]
        ON [projects].[PartDescriptions]([PartId] ASC);
END
GO

IF COL_LENGTH(N'projects.Activities', N'PartId') IS NULL
BEGIN
    ALTER TABLE [projects].[Activities] ADD [PartId] INT NULL;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Activities_Parts')
BEGIN
    ALTER TABLE [projects].[Activities] WITH CHECK
        ADD CONSTRAINT [FK_Activities_Parts] FOREIGN KEY ([PartId]) REFERENCES [projects].[Parts] ([Id]) ON DELETE SET NULL;
END
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
SET XACT_ABORT ON;

IF OBJECT_ID(N'dbo.SplitAlternativePartNumbers', N'TF') IS NULL
    THROW 51000, 'dbo.SplitAlternativePartNumbers must exist before the part backfill.', 1;

BEGIN TRANSACTION;

INSERT INTO projects.Parts (PartNo)
SELECT MIN(Piece.PartValue)
FROM
(
    SELECT LTRIM(RTRIM(A.PartNo)) AS PartNo
    FROM projects.Activities AS A
    WHERE NULLIF(LTRIM(RTRIM(A.PartNo)), N'') IS NOT NULL
    UNION ALL
    SELECT LTRIM(RTRIM(I.PartNo))
    FROM projects.QuotationItems AS I
    WHERE NULLIF(LTRIM(RTRIM(I.PartNo)), N'') IS NOT NULL
    UNION ALL
    SELECT LTRIM(RTRIM(I.PartNo))
    FROM projects.PurchaseOrderItems AS I
    WHERE NULLIF(LTRIM(RTRIM(I.PartNo)), N'') IS NOT NULL
) AS Source
CROSS APPLY dbo.SplitAlternativePartNumbers(Source.PartNo) AS Piece
WHERE NOT EXISTS
(
    SELECT 1
    FROM projects.Parts AS P
    WHERE HASHBYTES('SHA2_256', LOWER(LTRIM(RTRIM(CAST(P.PartNo AS NVARCHAR(4000))))))
        = HASHBYTES('SHA2_256', LOWER(LTRIM(RTRIM(CAST(Piece.PartValue AS NVARCHAR(4000))))))
      AND (SELECT COUNT(*) FROM dbo.SplitAlternativePartNumbers(P.PartNo)) = 1
)
GROUP BY HASHBYTES('SHA2_256', LOWER(LTRIM(RTRIM(CAST(Piece.PartValue AS NVARCHAR(4000))))));

DECLARE @InsertedParts INT = @@ROWCOUNT;

INSERT INTO projects.PartDescriptions (PartId, Description)
SELECT Picked.PartId, MIN(S.Description)
FROM
(
    SELECT
        PieceNo = Main.PartValue,
        Description = LTRIM(RTRIM(A.Description)),
        DescriptionKey = HASHBYTES('SHA2_256', LOWER(LTRIM(RTRIM(CAST(A.Description AS NVARCHAR(4000))))))
    FROM projects.Activities AS A
    CROSS APPLY
    (
        SELECT TOP (1) PartValue
        FROM dbo.SplitAlternativePartNumbers(A.PartNo)
        ORDER BY SortOrder
    ) AS Main
    WHERE NULLIF(LTRIM(RTRIM(A.PartNo)), N'') IS NOT NULL
      AND NULLIF(LTRIM(RTRIM(A.Description)), N'') IS NOT NULL

    UNION ALL

    SELECT
        PieceNo = CASE
            WHEN QI.IsAlternativePart = 1 AND Alt.PartValue IS NOT NULL THEN Alt.PartValue
            WHEN Exact.PartValue IS NOT NULL THEN Exact.PartValue
            ELSE Main.PartValue
        END,
        Description = LTRIM(RTRIM(QI.PartDescription)),
        DescriptionKey = HASHBYTES('SHA2_256', LOWER(LTRIM(RTRIM(CAST(QI.PartDescription AS NVARCHAR(4000))))))
    FROM projects.QuotationItems AS QI
    LEFT JOIN projects.ActivityAlternativeParts AS AP
        ON AP.Id = QI.AlternativePartId
    OUTER APPLY
    (
        SELECT TOP (1) Piece.PartValue
        FROM dbo.SplitAlternativePartNumbers(QI.PartNo) AS Piece
        WHERE HASHBYTES('SHA2_256', LOWER(LTRIM(RTRIM(CAST(Piece.PartValue AS NVARCHAR(4000))))))
            = HASHBYTES('SHA2_256', LOWER(LTRIM(RTRIM(CAST(QI.PartNo AS NVARCHAR(4000))))))
    ) AS Exact
    OUTER APPLY
    (
        SELECT TOP (1) PartValue
        FROM dbo.SplitAlternativePartNumbers(QI.PartNo)
        ORDER BY SortOrder
    ) AS Main
    OUTER APPLY
    (
        SELECT TOP (1) Piece.PartValue
        FROM dbo.SplitAlternativePartNumbers(QI.PartNo) AS Piece
        WHERE AP.AlternativePartNo IS NOT NULL
          AND HASHBYTES('SHA2_256', LOWER(LTRIM(RTRIM(CAST(Piece.PartValue AS NVARCHAR(4000))))))
            = HASHBYTES('SHA2_256', LOWER(LTRIM(RTRIM(CAST(AP.AlternativePartNo AS NVARCHAR(4000))))))
        ORDER BY Piece.SortOrder
    ) AS Alt
    WHERE NULLIF(LTRIM(RTRIM(QI.PartNo)), N'') IS NOT NULL
      AND NULLIF(LTRIM(RTRIM(QI.PartDescription)), N'') IS NOT NULL

    UNION ALL

    SELECT
        PieceNo = COALESCE(Exact.PartValue, Main.PartValue),
        Description = LTRIM(RTRIM(POI.PartDescription)),
        DescriptionKey = HASHBYTES('SHA2_256', LOWER(LTRIM(RTRIM(CAST(POI.PartDescription AS NVARCHAR(4000))))))
    FROM projects.PurchaseOrderItems AS POI
    OUTER APPLY
    (
        SELECT TOP (1) Piece.PartValue
        FROM dbo.SplitAlternativePartNumbers(POI.PartNo) AS Piece
        WHERE HASHBYTES('SHA2_256', LOWER(LTRIM(RTRIM(CAST(Piece.PartValue AS NVARCHAR(4000))))))
            = HASHBYTES('SHA2_256', LOWER(LTRIM(RTRIM(CAST(POI.PartNo AS NVARCHAR(4000))))))
    ) AS Exact
    OUTER APPLY
    (
        SELECT TOP (1) PartValue
        FROM dbo.SplitAlternativePartNumbers(POI.PartNo)
        ORDER BY SortOrder
    ) AS Main
    WHERE NULLIF(LTRIM(RTRIM(POI.PartNo)), N'') IS NOT NULL
      AND NULLIF(LTRIM(RTRIM(POI.PartDescription)), N'') IS NOT NULL
) AS S
CROSS APPLY
(
    SELECT TOP (1) P.Id AS PartId
    FROM projects.Parts AS P
    WHERE HASHBYTES('SHA2_256', LOWER(LTRIM(RTRIM(CAST(P.PartNo AS NVARCHAR(4000))))))
        = HASHBYTES('SHA2_256', LOWER(LTRIM(RTRIM(CAST(S.PieceNo AS NVARCHAR(4000))))))
      AND (SELECT COUNT(*) FROM dbo.SplitAlternativePartNumbers(P.PartNo)) = 1
    ORDER BY P.Id
) AS Picked
WHERE NULLIF(S.PieceNo, N'') IS NOT NULL
  AND NOT EXISTS
  (
      SELECT 1
      FROM projects.PartDescriptions AS D
      WHERE D.PartId = Picked.PartId
        AND HASHBYTES('SHA2_256', LOWER(LTRIM(RTRIM(CAST(D.Description AS NVARCHAR(4000)))))) = S.DescriptionKey
  )
GROUP BY Picked.PartId, S.DescriptionKey;

DECLARE @InsertedDescriptions INT = @@ROWCOUNT;

UPDATE A
SET PartId = Picked.PartId
FROM projects.Activities AS A
CROSS APPLY
(
    SELECT TOP (1) PartValue
    FROM dbo.SplitAlternativePartNumbers(A.PartNo)
    ORDER BY SortOrder
) AS Main
CROSS APPLY
(
    SELECT TOP (1) P.Id AS PartId
    FROM projects.Parts AS P
    WHERE HASHBYTES('SHA2_256', LOWER(LTRIM(RTRIM(CAST(P.PartNo AS NVARCHAR(4000))))))
        = HASHBYTES('SHA2_256', LOWER(LTRIM(RTRIM(CAST(Main.PartValue AS NVARCHAR(4000))))))
      AND (SELECT COUNT(*) FROM dbo.SplitAlternativePartNumbers(P.PartNo)) = 1
    ORDER BY P.Id
) AS Picked
WHERE A.PartId IS NULL
  AND NULLIF(LTRIM(RTRIM(A.PartNo)), N'') IS NOT NULL;

DECLARE @LinkedActivities INT = @@ROWCOUNT;

DECLARE @AlternateLinks INT = 0;
EXEC dbo.SeedPartAlternates @InsertedLinks = @AlternateLinks OUTPUT;

COMMIT TRANSACTION;

SELECT
    @InsertedParts AS InsertedParts,
    @InsertedDescriptions AS InsertedDescriptions,
    @LinkedActivities AS LinkedActivities,
    @AlternateLinks AS AlternateLinks,
    (SELECT COUNT(*) FROM projects.Parts) AS Parts,
    (SELECT COUNT(*) FROM projects.PartDescriptions) AS Descriptions,
    (
        SELECT COUNT(*)
        FROM projects.Parts AS P
        WHERE (SELECT COUNT(*) FROM dbo.SplitAlternativePartNumbers(P.PartNo)) > 1
    ) AS CombinedPartsRemaining,
    (
        SELECT COUNT(*)
        FROM projects.Activities AS A
        WHERE A.PartId IS NULL
          AND NULLIF(LTRIM(RTRIM(A.PartNo)), N'') IS NOT NULL
    ) AS ActivitiesUnlinked;
GO
