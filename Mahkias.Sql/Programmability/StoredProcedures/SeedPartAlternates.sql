-- Links the main piece of a combined part number to each later piece.
-- Sources are saved part numbers on activities, quotations, purchase orders,
-- and any Part row that still contains or, (or), or a slash between full part numbers.
-- The first piece is the main part. Later pieces are part-level alternates.
-- Project alternate rows in ActivityAlternativeParts are not read or written.
CREATE PROCEDURE [dbo].[SeedPartAlternates]
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
