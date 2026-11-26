SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

-- Repair pass for Part rows that still contain or, (or), or a slash between
-- two full part numbers. AddParts.sql does not insert those combined values.
-- Run this only when CheckPartBackfill.sql reports CombinedPartsRemaining
-- greater than 0. Then run SeedPartAlternates.sql and CheckPartBackfill.sql.
-- Re-running is safe. The split rule is dbo.SplitAlternativePartNumbers.
-- Activity text, quotation lines, and purchase-order lines keep the part number
-- that was saved on them.
-- Activities that still point at a combined Part row are moved to the main piece.
-- Quotation and purchase-order descriptions are attached to the piece their
-- part number matches: the main piece, or an alternate piece when that is the
-- number on the line. Combined Part rows are removed only when nothing still
-- references them.
-- When dbo.SeedPartAlternates is already installed, this script links the
-- main piece to each later piece. Run SeedPartAlternates.sql after this
-- script as well. Re-running that script does not add duplicate links.

BEGIN TRANSACTION;

DECLARE @Combined TABLE
(
    CombinedId INT IDENTITY (1, 1) NOT NULL PRIMARY KEY,
    PartNo NVARCHAR(MAX) NOT NULL,
    PartKey VARBINARY(32) NOT NULL
);

INSERT INTO @Combined (PartNo, PartKey)
SELECT MIN(S.PartNo), S.PartKey
FROM
(
    SELECT
        LTRIM(RTRIM(PartNo)) AS PartNo,
        HASHBYTES('SHA2_256', LOWER(LTRIM(RTRIM(CAST(PartNo AS NVARCHAR(4000)))))) AS PartKey
    FROM projects.Parts
    WHERE NULLIF(LTRIM(RTRIM(PartNo)), N'') IS NOT NULL
    UNION ALL
    SELECT
        LTRIM(RTRIM(PartNo)),
        HASHBYTES('SHA2_256', LOWER(LTRIM(RTRIM(CAST(PartNo AS NVARCHAR(4000))))))
    FROM projects.Activities
    WHERE NULLIF(LTRIM(RTRIM(PartNo)), N'') IS NOT NULL
    UNION ALL
    SELECT
        LTRIM(RTRIM(PartNo)),
        HASHBYTES('SHA2_256', LOWER(LTRIM(RTRIM(CAST(PartNo AS NVARCHAR(4000))))))
    FROM projects.QuotationItems
    WHERE NULLIF(LTRIM(RTRIM(PartNo)), N'') IS NOT NULL
    UNION ALL
    SELECT
        LTRIM(RTRIM(PartNo)),
        HASHBYTES('SHA2_256', LOWER(LTRIM(RTRIM(CAST(PartNo AS NVARCHAR(4000))))))
    FROM projects.PurchaseOrderItems
    WHERE NULLIF(LTRIM(RTRIM(PartNo)), N'') IS NOT NULL
) AS S
GROUP BY S.PartKey;

DELETE C
FROM @Combined AS C
WHERE (SELECT COUNT(*) FROM dbo.SplitAlternativePartNumbers(C.PartNo)) < 2;

DECLARE @Piece TABLE
(
    CombinedId INT NOT NULL,
    SortOrder INT NOT NULL,
    PartValue NVARCHAR(MAX) NOT NULL,
    PartKey VARBINARY(32) NOT NULL,
    PartId INT NULL,
    PRIMARY KEY (CombinedId, SortOrder)
);

DECLARE @CombinedId INT;
DECLARE @PartNo NVARCHAR(MAX);

DECLARE combined_cursor CURSOR LOCAL FAST_FORWARD FOR
    SELECT CombinedId, PartNo
    FROM @Combined
    ORDER BY CombinedId;

OPEN combined_cursor;
FETCH NEXT FROM combined_cursor INTO @CombinedId, @PartNo;

WHILE @@FETCH_STATUS = 0
BEGIN
    INSERT INTO @Piece (CombinedId, SortOrder, PartValue, PartKey)
    SELECT
        @CombinedId,
        SortOrder,
        PartValue,
        HASHBYTES('SHA2_256', LOWER(LTRIM(RTRIM(CAST(PartValue AS NVARCHAR(4000))))))
    FROM dbo.SplitAlternativePartNumbers(@PartNo);

    FETCH NEXT FROM combined_cursor INTO @CombinedId, @PartNo;
END

CLOSE combined_cursor;
DEALLOCATE combined_cursor;

DECLARE @CreatedParts INT = 0;
DECLARE @ReusedParts INT = 0;
DECLARE @PartKey VARBINARY(32);
DECLARE @PartValue NVARCHAR(MAX);
DECLARE @TargetId INT;

DECLARE piece_cursor CURSOR LOCAL FAST_FORWARD FOR
    SELECT PartKey, MIN(PartValue) AS PartValue
    FROM @Piece
    GROUP BY PartKey
    ORDER BY MIN(SortOrder), MIN(PartValue);

OPEN piece_cursor;
FETCH NEXT FROM piece_cursor INTO @PartKey, @PartValue;

WHILE @@FETCH_STATUS = 0
BEGIN
    SET @TargetId = NULL;

    SELECT TOP (1) @TargetId = P.Id
    FROM projects.Parts AS P
    WHERE HASHBYTES('SHA2_256', LOWER(LTRIM(RTRIM(CAST(P.PartNo AS NVARCHAR(4000)))))) = @PartKey
      AND (SELECT COUNT(*) FROM dbo.SplitAlternativePartNumbers(P.PartNo)) = 1
    ORDER BY P.Id;

    IF @TargetId IS NULL
    BEGIN
        INSERT INTO projects.Parts (PartNo)
        VALUES (@PartValue);

        SET @TargetId = CAST(SCOPE_IDENTITY() AS INT);
        SET @CreatedParts = @CreatedParts + 1;
    END
    ELSE
    BEGIN
        SET @ReusedParts = @ReusedParts + 1;
    END

    UPDATE @Piece
    SET PartId = @TargetId
    WHERE PartKey = @PartKey;

    FETCH NEXT FROM piece_cursor INTO @PartKey, @PartValue;
END

CLOSE piece_cursor;
DEALLOCATE piece_cursor;

DECLARE @DescriptionsAdded INT = 0;

INSERT INTO projects.PartDescriptions (PartId, Description)
SELECT Piece.PartId, MIN(D.Description)
FROM projects.Parts AS Source
INNER JOIN @Combined AS C
    ON C.PartKey = HASHBYTES('SHA2_256', LOWER(LTRIM(RTRIM(CAST(Source.PartNo AS NVARCHAR(4000))))))
INNER JOIN @Piece AS Piece
    ON Piece.CombinedId = C.CombinedId
INNER JOIN projects.PartDescriptions AS D
    ON D.PartId = Source.Id
WHERE (SELECT COUNT(*) FROM dbo.SplitAlternativePartNumbers(Source.PartNo)) > 1
  AND NULLIF(LTRIM(RTRIM(D.Description)), N'') IS NOT NULL
  AND NOT EXISTS
  (
      SELECT 1
      FROM projects.PartDescriptions AS Existing
      WHERE Existing.PartId = Piece.PartId
        AND LOWER(LTRIM(RTRIM(Existing.Description))) = LOWER(LTRIM(RTRIM(D.Description)))
  )
GROUP BY Piece.PartId, LOWER(LTRIM(RTRIM(D.Description)));

SET @DescriptionsAdded = @DescriptionsAdded + @@ROWCOUNT;

DECLARE @MovedActivities INT = 0;

UPDATE A
SET PartId = Main.PartId
FROM projects.Activities AS A
INNER JOIN @Combined AS C
    ON C.PartKey = HASHBYTES('SHA2_256', LOWER(LTRIM(RTRIM(CAST(A.PartNo AS NVARCHAR(4000))))))
INNER JOIN @Piece AS Main
    ON Main.CombinedId = C.CombinedId
   AND Main.SortOrder = 1
WHERE A.PartId IS NULL
   OR EXISTS
   (
       SELECT 1
       FROM projects.Parts AS P
       WHERE P.Id = A.PartId
         AND (SELECT COUNT(*) FROM dbo.SplitAlternativePartNumbers(P.PartNo)) > 1
   );

SET @MovedActivities = @@ROWCOUNT;

INSERT INTO projects.PartDescriptions (PartId, Description)
SELECT Target.PartId, MIN(Line.Description)
FROM
(
    SELECT
        TargetPartId = CASE
            WHEN QI.IsAlternativePart = 1
             AND AltPiece.PartId IS NOT NULL THEN AltPiece.PartId
            ELSE COALESCE(Exact.PartId, Main.PartId)
        END,
        Description = LTRIM(RTRIM(QI.PartDescription))
    FROM projects.QuotationItems AS QI
    LEFT JOIN projects.ActivityAlternativeParts AS AP
        ON AP.Id = QI.AlternativePartId
    LEFT JOIN @Piece AS Exact
        ON Exact.PartKey = HASHBYTES('SHA2_256', LOWER(LTRIM(RTRIM(CAST(QI.PartNo AS NVARCHAR(4000))))))
    LEFT JOIN @Combined AS C
        ON C.PartKey = HASHBYTES('SHA2_256', LOWER(LTRIM(RTRIM(CAST(QI.PartNo AS NVARCHAR(4000))))))
    LEFT JOIN @Piece AS Main
        ON Main.CombinedId = C.CombinedId
       AND Main.SortOrder = 1
    LEFT JOIN @Piece AS AltPiece
        ON AltPiece.CombinedId = C.CombinedId
       AND AltPiece.PartKey = HASHBYTES('SHA2_256', LOWER(LTRIM(RTRIM(CAST(AP.AlternativePartNo AS NVARCHAR(4000))))))
    WHERE NULLIF(LTRIM(RTRIM(QI.PartDescription)), N'') IS NOT NULL
      AND (Exact.PartId IS NOT NULL OR Main.PartId IS NOT NULL)
    UNION ALL
    SELECT
        TargetPartId = COALESCE(Exact.PartId, Main.PartId),
        Description = LTRIM(RTRIM(POI.PartDescription))
    FROM projects.PurchaseOrderItems AS POI
    LEFT JOIN @Piece AS Exact
        ON Exact.PartKey = HASHBYTES('SHA2_256', LOWER(LTRIM(RTRIM(CAST(POI.PartNo AS NVARCHAR(4000))))))
    LEFT JOIN @Combined AS C
        ON C.PartKey = HASHBYTES('SHA2_256', LOWER(LTRIM(RTRIM(CAST(POI.PartNo AS NVARCHAR(4000))))))
    LEFT JOIN @Piece AS Main
        ON Main.CombinedId = C.CombinedId
       AND Main.SortOrder = 1
    WHERE NULLIF(LTRIM(RTRIM(POI.PartDescription)), N'') IS NOT NULL
      AND (Exact.PartId IS NOT NULL OR Main.PartId IS NOT NULL)
) AS Line
INNER JOIN
(
    SELECT PartId
    FROM @Piece
    GROUP BY PartId
) AS Target
    ON Target.PartId = Line.TargetPartId
WHERE NULLIF(Line.Description, N'') IS NOT NULL
  AND NOT EXISTS
  (
      SELECT 1
      FROM projects.PartDescriptions AS Existing
      WHERE Existing.PartId = Line.TargetPartId
        AND LOWER(LTRIM(RTRIM(Existing.Description))) = LOWER(Line.Description)
  )
GROUP BY Target.PartId, LOWER(Line.Description);

SET @DescriptionsAdded = @DescriptionsAdded + @@ROWCOUNT;

DECLARE @RemovedCombinedParts INT = 0;
DECLARE @SkippedCombinedParts INT = 0;
DECLARE @SourceId INT;
DECLARE @Blocked INT;
DECLARE @Check NVARCHAR(MAX);

DECLARE source_cursor CURSOR LOCAL FAST_FORWARD FOR
    SELECT P.Id
    FROM projects.Parts AS P
    WHERE (SELECT COUNT(*) FROM dbo.SplitAlternativePartNumbers(P.PartNo)) > 1
    ORDER BY P.Id;

OPEN source_cursor;
FETCH NEXT FROM source_cursor INTO @SourceId;

WHILE @@FETCH_STATUS = 0
BEGIN
    SET @Blocked = 0;
    SET @Check = N'SET @Blocked = 0;';

    SELECT @Check = @Check + N'
IF EXISTS (SELECT 1 FROM ' + QUOTENAME(SCHEMA_NAME(parent.schema_id)) + N'.' + QUOTENAME(parent.name) + N' WHERE ' + QUOTENAME(col.name) + N' = @SourceId)
    SET @Blocked = @Blocked + 1;'
    FROM sys.foreign_keys AS fk
    INNER JOIN sys.foreign_key_columns AS fkc
        ON fkc.constraint_object_id = fk.object_id
    INNER JOIN sys.tables AS parent
        ON parent.object_id = fk.parent_object_id
    INNER JOIN sys.columns AS col
        ON col.object_id = parent.object_id
       AND col.column_id = fkc.parent_column_id
    WHERE fk.referenced_object_id = OBJECT_ID(N'projects.Parts')
      AND parent.object_id <> OBJECT_ID(N'projects.PartDescriptions');

    EXEC sys.sp_executesql
        @Check,
        N'@SourceId INT, @Blocked INT OUTPUT',
        @SourceId = @SourceId,
        @Blocked = @Blocked OUTPUT;

    IF @Blocked = 0
    BEGIN
        DELETE FROM projects.PartDescriptions
        WHERE PartId = @SourceId;

        DELETE FROM projects.Parts
        WHERE Id = @SourceId;

        SET @RemovedCombinedParts = @RemovedCombinedParts + 1;
    END
    ELSE
    BEGIN
        SET @SkippedCombinedParts = @SkippedCombinedParts + 1;
    END

    FETCH NEXT FROM source_cursor INTO @SourceId;
END

CLOSE source_cursor;
DEALLOCATE source_cursor;

DECLARE @AlternateLinks INT = 0;
IF OBJECT_ID(N'dbo.SeedPartAlternates', N'P') IS NOT NULL
    EXEC dbo.SeedPartAlternates @InsertedLinks = @AlternateLinks OUTPUT;

COMMIT TRANSACTION;

SELECT
    @CreatedParts AS CreatedParts,
    @ReusedParts AS ReusedParts,
    @MovedActivities AS MovedActivities,
    @DescriptionsAdded AS DescriptionsAdded,
    @RemovedCombinedParts AS RemovedCombinedParts,
    @SkippedCombinedParts AS SkippedCombinedParts,
    @AlternateLinks AS AlternateLinks;
GO
