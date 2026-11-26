CREATE PROCEDURE [dbo].[ListPartQuotations]
(
    @PartId INT
)
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @MainKey VARBINARY(32) =
    (
        SELECT HASHBYTES('SHA2_256', LOWER(LTRIM(RTRIM(CAST(PartNo AS NVARCHAR(4000))))))
        FROM projects.Parts
        WHERE Id = @PartId
    );

    DECLARE @Keys TABLE
    (
        PartKey VARBINARY(32) NOT NULL PRIMARY KEY
    );

    INSERT INTO @Keys (PartKey)
    SELECT HASHBYTES('SHA2_256', LOWER(LTRIM(RTRIM(CAST(P.PartNo AS NVARCHAR(4000))))))
    FROM projects.Parts AS P
    WHERE P.Id = @PartId
    UNION
    SELECT HASHBYTES('SHA2_256', LOWER(LTRIM(RTRIM(CAST(Alt.PartNo AS NVARCHAR(4000))))))
    FROM projects.PartAlternates AS L
    INNER JOIN projects.Parts AS Alt
        ON Alt.Id = L.AlternatePartId
    WHERE L.PartId = @PartId;

    SELECT
        QuotationItemId = QI.Id,
        QuotationId = Q.Id,
        Q.Code,
        SupplierId = S.Id,
        SupplierName = S.Name,
        Q.SupplierReference,
        QI.PartNo,
        QI.Quantity,
        Unit = COALESCE(NULLIF(LTRIM(RTRIM(U.Code)), N''), NULLIF(LTRIM(RTRIM(U.Name)), N'')),
        QI.Condition,
        QI.UnitPrice,
        QI.TotalPrice,
        CurrencyCode = C.Code,
        CurrencySymbol = C.Symbol,
        Q.QuotedDate,
        Q.ValidUntil,
        Match = CASE
            WHEN EXISTS
            (
                SELECT 1
                FROM dbo.SplitAlternativePartNumbers(QI.PartNo) AS Piece
                WHERE HASHBYTES('SHA2_256', LOWER(LTRIM(RTRIM(CAST(Piece.PartValue AS NVARCHAR(4000)))))) = @MainKey
            ) THEN N'Main'
            ELSE N'Alternate'
        END,
        Projects =
        (
            SELECT
                P.Id AS id,
                label = COALESCE(NULLIF(LTRIM(RTRIM(P.Reference)), N''), P.Name)
            FROM projects.QuotationProjects AS QP
            INNER JOIN projects.Projects AS P
                ON P.Id = QP.ProjectId
               AND P.IsDeleted = 0
            WHERE QP.QuotationId = Q.Id
            ORDER BY label, P.Id
            FOR JSON PATH
        )
    FROM projects.QuotationItems AS QI
    INNER JOIN projects.Quotations AS Q
        ON Q.Id = QI.QuotationId
       AND Q.IsDeleted = 0
    INNER JOIN projects.Suppliers AS S
        ON S.Id = Q.SupplierId
    INNER JOIN dbo.Currencies AS C
        ON C.Id = QI.CurrencyId
    LEFT JOIN dbo.Units AS U
        ON U.Id = QI.UnitId
    WHERE EXISTS
    (
        SELECT 1
        FROM dbo.SplitAlternativePartNumbers(QI.PartNo) AS Piece
        INNER JOIN @Keys AS K
            ON K.PartKey = HASHBYTES('SHA2_256', LOWER(LTRIM(RTRIM(CAST(Piece.PartValue AS NVARCHAR(4000))))))
    )
    ORDER BY Q.QuotedDate DESC, Q.Id DESC, QI.Id;
END
