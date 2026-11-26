-- Read-only checks for the Part backfill. This script does not insert, update, or delete.
--
-- Production order, after dbo.SplitAlternativePartNumbers is already installed:
--   1. Run this script and save the results.
--   2. Run AddParts.sql. It creates Parts, PartDescriptions, and PartAlternates,
--      links activities to the main piece, and seeds part-level alternates.
--   3. Run SetPartDeleteBehavior.sql.
--   4. Run SeedPartAlternates.sql. Re-running it adds no duplicate links.
--   5. Run this script again.
--   6. If CombinedPartsRemaining is greater than 0, run SplitCombinedOrParts.sql,
--      run SeedPartAlternates.sql again, then run this script once more.
--
-- After the last run: CombinedPartsRemaining, ActivitiesUnlinked,
-- OrphanActivityLinks, DuplicateDescriptions, SelfLinks, DuplicateLinks,
-- and MissingAlternateLinks are all 0.
-- Section 1 and section 2 counts must match the first run. Saved activity,
-- quotation, and purchase-order part numbers are not rewritten, and
-- ProjectAlternateRows is the project-level alternate table, which these
-- scripts do not change.
SET NOCOUNT ON;

IF OBJECT_ID(N'dbo.SplitAlternativePartNumbers', N'TF') IS NULL
    THROW 51000, 'dbo.SplitAlternativePartNumbers must exist before these checks.', 1;

PRINT '1. Source rows';
SELECT
    (SELECT COUNT(*) FROM projects.Activities WHERE NULLIF(LTRIM(RTRIM(PartNo)), N'') IS NOT NULL) AS ActivitiesWithPartNo,
    (SELECT COUNT(*) FROM projects.QuotationItems WHERE NULLIF(LTRIM(RTRIM(PartNo)), N'') IS NOT NULL) AS QuotationLinesWithPartNo,
    (SELECT COUNT(*) FROM projects.PurchaseOrderItems WHERE NULLIF(LTRIM(RTRIM(PartNo)), N'') IS NOT NULL) AS PurchaseOrderLinesWithPartNo,
    (SELECT COUNT(*) FROM projects.ActivityAlternativeParts) AS ProjectAlternateRows;

PRINT '2. Rows whose part number splits into a main piece and one or more alternates';
SELECT SourceName, COUNT(*) AS RowsThatSplit
FROM
(
    SELECT 'Activity' AS SourceName, A.PartNo
    FROM projects.Activities AS A
    WHERE (SELECT COUNT(*) FROM dbo.SplitAlternativePartNumbers(A.PartNo)) > 1
    UNION ALL
    SELECT 'Quotation', I.PartNo
    FROM projects.QuotationItems AS I
    WHERE (SELECT COUNT(*) FROM dbo.SplitAlternativePartNumbers(I.PartNo)) > 1
    UNION ALL
    SELECT 'PurchaseOrder', I.PartNo
    FROM projects.PurchaseOrderItems AS I
    WHERE (SELECT COUNT(*) FROM dbo.SplitAlternativePartNumbers(I.PartNo)) > 1
) AS S
GROUP BY SourceName;

PRINT '3. Split examples (original text is not changed by the backfill)';
SELECT TOP (100)
    SourceName,
    SourceId,
    PartNo,
    SortOrder,
    PartValue,
    CASE WHEN SortOrder = 1 THEN 'Main' ELSE 'Alt' END AS PieceRole
FROM
(
    SELECT 'Activity' AS SourceName, A.Id AS SourceId, A.PartNo
    FROM projects.Activities AS A
    WHERE (SELECT COUNT(*) FROM dbo.SplitAlternativePartNumbers(A.PartNo)) > 1
    UNION ALL
    SELECT 'Quotation', I.Id, I.PartNo
    FROM projects.QuotationItems AS I
    WHERE (SELECT COUNT(*) FROM dbo.SplitAlternativePartNumbers(I.PartNo)) > 1
    UNION ALL
    SELECT 'PurchaseOrder', I.Id, I.PartNo
    FROM projects.PurchaseOrderItems AS I
    WHERE (SELECT COUNT(*) FROM dbo.SplitAlternativePartNumbers(I.PartNo)) > 1
) AS S
CROSS APPLY dbo.SplitAlternativePartNumbers(S.PartNo) AS Piece
ORDER BY SourceName, SourceId, SortOrder;

PRINT '4. Slash part numbers kept as one piece (fraction, short suffix, or one side is not a full part number)';
SELECT TOP (100)
    SourceName,
    SourceId,
    PartNo
FROM
(
    SELECT 'Activity' AS SourceName, A.Id AS SourceId, A.PartNo
    FROM projects.Activities AS A
    UNION ALL
    SELECT 'Quotation', I.Id, I.PartNo
    FROM projects.QuotationItems AS I
    UNION ALL
    SELECT 'PurchaseOrder', I.Id, I.PartNo
    FROM projects.PurchaseOrderItems AS I
) AS S
WHERE S.PartNo LIKE N'%/%'
  AND (SELECT COUNT(*) FROM dbo.SplitAlternativePartNumbers(S.PartNo)) = 1
ORDER BY SourceName, SourceId;

PRINT '5. Quotation lines and the piece their description will be attached to';
SELECT TOP (100)
    QI.Id AS QuotationItemId,
    QI.PartNo,
    QI.IsAlternativePart,
    AP.AlternativePartNo,
    MappedPiece = CASE
        WHEN QI.IsAlternativePart = 1 AND Alt.PartValue IS NOT NULL THEN Alt.PartValue
        WHEN Exact.PartValue IS NOT NULL THEN Exact.PartValue
        ELSE Main.PartValue
    END,
    PieceRole = CASE
        WHEN QI.IsAlternativePart = 1 AND Alt.PartValue IS NOT NULL THEN 'Alt'
        WHEN Exact.PartValue IS NOT NULL AND Main.PartValue IS NOT NULL AND Exact.PartValue <> Main.PartValue THEN 'Alt'
        ELSE 'Main'
    END
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
  AND (SELECT COUNT(*) FROM dbo.SplitAlternativePartNumbers(QI.PartNo)) > 1
ORDER BY QI.Id;

PRINT '6. Purchase-order lines and the piece their description will be attached to';
SELECT TOP (100)
    POI.Id AS PurchaseOrderItemId,
    POI.PartNo,
    MappedPiece = COALESCE(Exact.PartValue, Main.PartValue),
    PieceRole = CASE
        WHEN Exact.PartValue IS NOT NULL AND Main.PartValue IS NOT NULL AND Exact.PartValue <> Main.PartValue THEN 'Alt'
        ELSE 'Main'
    END
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
  AND (SELECT COUNT(*) FROM dbo.SplitAlternativePartNumbers(POI.PartNo)) > 1
ORDER BY POI.Id;

PRINT '7. Distinct part numbers the backfill will use';
SELECT COUNT(*) AS DistinctPartPieces
FROM
(
    SELECT HASHBYTES('SHA2_256', LOWER(LTRIM(RTRIM(CAST(Piece.PartValue AS NVARCHAR(4000)))))) AS PartKey
    FROM
    (
        SELECT PartNo FROM projects.Activities WHERE NULLIF(LTRIM(RTRIM(PartNo)), N'') IS NOT NULL
        UNION ALL
        SELECT PartNo FROM projects.QuotationItems WHERE NULLIF(LTRIM(RTRIM(PartNo)), N'') IS NOT NULL
        UNION ALL
        SELECT PartNo FROM projects.PurchaseOrderItems WHERE NULLIF(LTRIM(RTRIM(PartNo)), N'') IS NOT NULL
    ) AS Source
    CROSS APPLY dbo.SplitAlternativePartNumbers(Source.PartNo) AS Piece
    GROUP BY HASHBYTES('SHA2_256', LOWER(LTRIM(RTRIM(CAST(Piece.PartValue AS NVARCHAR(4000))))))
) AS Pieces;

IF OBJECT_ID(N'projects.Parts', N'U') IS NULL
BEGIN
    PRINT '8. Parts table is not present yet. Run AddParts.sql, then run this script again.';
END
ELSE
BEGIN
    EXEC sys.sp_executesql N'
        PRINT ''8. Result after backfill'';
        SELECT
            (SELECT COUNT(*) FROM projects.Parts) AS Parts,
            (SELECT COUNT(*) FROM projects.ActivityAlternativeParts) AS ProjectAlternateRows,
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
                  AND NULLIF(LTRIM(RTRIM(A.PartNo)), N'''') IS NOT NULL
            ) AS ActivitiesUnlinked,
            (
                SELECT COUNT(*)
                FROM projects.Activities AS A
                WHERE A.PartId IS NOT NULL
                  AND NOT EXISTS (SELECT 1 FROM projects.Parts AS P WHERE P.Id = A.PartId)
            ) AS OrphanActivityLinks,
            (
                SELECT COUNT(*)
                FROM
                (
                    SELECT PartId
                    FROM projects.PartDescriptions
                    GROUP BY PartId, HASHBYTES(''SHA2_256'', LOWER(LTRIM(RTRIM(CAST(Description AS NVARCHAR(4000))))))
                    HAVING COUNT(*) > 1
                ) AS D
            ) AS DuplicateDescriptions;

        PRINT ''9. Combined Part rows still stored (should be empty)'';
        SELECT TOP (100)
            P.Id,
            P.PartNo,
            (SELECT COUNT(*) FROM projects.Activities AS A WHERE A.PartId = P.Id) AS Activities,
            (SELECT COUNT(*) FROM projects.PartDescriptions AS D WHERE D.PartId = P.Id) AS Descriptions
        FROM projects.Parts AS P
        WHERE (SELECT COUNT(*) FROM dbo.SplitAlternativePartNumbers(P.PartNo)) > 1
        ORDER BY P.Id;

        PRINT ''10. Activities whose saved part number splits, and the main part they reference'';
        SELECT TOP (100)
            A.Id AS ActivityId,
            A.PartNo AS SavedPartNo,
            Main.PartValue AS MainPiece,
            Linked.PartNo AS LinkedPartNo
        FROM projects.Activities AS A
        CROSS APPLY
        (
            SELECT TOP (1) PartValue
            FROM dbo.SplitAlternativePartNumbers(A.PartNo)
            ORDER BY SortOrder
        ) AS Main
        LEFT JOIN projects.Parts AS Linked
            ON Linked.Id = A.PartId
        WHERE (SELECT COUNT(*) FROM dbo.SplitAlternativePartNumbers(A.PartNo)) > 1
        ORDER BY A.Id;

        PRINT ''11. Single-token quotation or purchase-order part numbers with no Part row'';
        SELECT TOP (100)
            SourceName,
            SourceId,
            PartNo
        FROM
        (
            SELECT ''Quotation'' AS SourceName, I.Id AS SourceId, LTRIM(RTRIM(I.PartNo)) AS PartNo
            FROM projects.QuotationItems AS I
            WHERE NULLIF(LTRIM(RTRIM(I.PartNo)), N'''') IS NOT NULL
              AND (SELECT COUNT(*) FROM dbo.SplitAlternativePartNumbers(I.PartNo)) = 1
            UNION ALL
            SELECT ''PurchaseOrder'', I.Id, LTRIM(RTRIM(I.PartNo))
            FROM projects.PurchaseOrderItems AS I
            WHERE NULLIF(LTRIM(RTRIM(I.PartNo)), N'''') IS NOT NULL
              AND (SELECT COUNT(*) FROM dbo.SplitAlternativePartNumbers(I.PartNo)) = 1
        ) AS Line
        WHERE NOT EXISTS
        (
            SELECT 1
            FROM projects.Parts AS P
            WHERE HASHBYTES(''SHA2_256'', LOWER(LTRIM(RTRIM(CAST(P.PartNo AS NVARCHAR(4000))))))
                = HASHBYTES(''SHA2_256'', LOWER(LTRIM(RTRIM(CAST(Line.PartNo AS NVARCHAR(4000))))))
              AND (SELECT COUNT(*) FROM dbo.SplitAlternativePartNumbers(P.PartNo)) = 1
        )
        ORDER BY SourceName, SourceId;

        PRINT ''12. Part-level alternate links from combined part numbers'';
        IF OBJECT_ID(N''projects.PartAlternates'', N''U'') IS NULL
        BEGIN
            PRINT ''PartAlternates is not present yet. Run SeedPartAlternates.sql.'';
        END
        ELSE
        BEGIN
            SELECT
                (
                    SELECT COUNT(*)
                    FROM projects.PartAlternates
                ) AS StoredLinks,
                (
                    SELECT COUNT(*)
                    FROM projects.PartAlternates
                    WHERE PartId = AlternatePartId
                ) AS SelfLinks,
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
                (
                    SELECT COUNT(*)
                    FROM
                    (
                        SELECT DISTINCT MainPart.Id AS MainPartId, AltPart.Id AS AlternatePartId
                        FROM
                        (
                            SELECT LTRIM(RTRIM(A.PartNo)) AS PartNo
                            FROM projects.Activities AS A
                            WHERE NULLIF(LTRIM(RTRIM(A.PartNo)), N'''') IS NOT NULL
                            UNION
                            SELECT LTRIM(RTRIM(I.PartNo))
                            FROM projects.QuotationItems AS I
                            WHERE NULLIF(LTRIM(RTRIM(I.PartNo)), N'''') IS NOT NULL
                            UNION
                            SELECT LTRIM(RTRIM(I.PartNo))
                            FROM projects.PurchaseOrderItems AS I
                            WHERE NULLIF(LTRIM(RTRIM(I.PartNo)), N'''') IS NOT NULL
                            UNION
                            SELECT LTRIM(RTRIM(P.PartNo))
                            FROM projects.Parts AS P
                            WHERE NULLIF(LTRIM(RTRIM(P.PartNo)), N'''') IS NOT NULL
                        ) AS Source
                        CROSS APPLY dbo.SplitAlternativePartNumbers(Source.PartNo) AS MainPiece
                        CROSS APPLY dbo.SplitAlternativePartNumbers(Source.PartNo) AS AltPiece
                        CROSS APPLY
                        (
                            SELECT TOP (1) P.Id
                            FROM projects.Parts AS P
                            WHERE HASHBYTES(''SHA2_256'', LOWER(LTRIM(RTRIM(CAST(P.PartNo AS NVARCHAR(4000))))))
                                = HASHBYTES(''SHA2_256'', LOWER(LTRIM(RTRIM(CAST(MainPiece.PartValue AS NVARCHAR(4000))))))
                              AND (SELECT COUNT(*) FROM dbo.SplitAlternativePartNumbers(P.PartNo)) = 1
                            ORDER BY P.Id
                        ) AS MainPart
                        CROSS APPLY
                        (
                            SELECT TOP (1) P.Id
                            FROM projects.Parts AS P
                            WHERE HASHBYTES(''SHA2_256'', LOWER(LTRIM(RTRIM(CAST(P.PartNo AS NVARCHAR(4000))))))
                                = HASHBYTES(''SHA2_256'', LOWER(LTRIM(RTRIM(CAST(AltPiece.PartValue AS NVARCHAR(4000))))))
                              AND (SELECT COUNT(*) FROM dbo.SplitAlternativePartNumbers(P.PartNo)) = 1
                            ORDER BY P.Id
                        ) AS AltPart
                        WHERE MainPiece.IsMain = 1
                          AND AltPiece.IsMain = 0
                          AND MainPart.Id <> AltPart.Id
                          AND NOT EXISTS
                          (
                              SELECT 1
                              FROM projects.PartAlternates AS Existing
                              WHERE Existing.PartId = MainPart.Id
                                AND Existing.AlternatePartId = AltPart.Id
                          )
                    ) AS Missing
                ) AS MissingAlternateLinks;
        END
    ';
END
GO
