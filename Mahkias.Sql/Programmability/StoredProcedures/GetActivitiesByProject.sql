CREATE OR ALTER PROCEDURE [dbo].[GetActivitiesByProject]
(
    @ProjectId INT,
    @SortBy NVARCHAR(50) = N'createdAt',
    @SortDirection NVARCHAR(4) = N'desc'
)
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @SortColumn NVARCHAR(50) = LOWER(ISNULL(NULLIF(LTRIM(RTRIM(@SortBy)), N''), N'createdAt'));
    DECLARE @SortDesc BIT = CASE
        WHEN LOWER(LTRIM(RTRIM(ISNULL(@SortDirection, N'desc')))) = N'asc' THEN 0
        ELSE 1
    END;

    IF @SortColumn IN (N'dsn', N'dsnno')
        SET @SortColumn = N'dsnno';
    IF @SortColumn IN (N'part', N'partno')
        SET @SortColumn = N'partno';
    IF @SortColumn = N'qty'
        SET @SortColumn = N'quantity';

    IF @SortColumn NOT IN (N'dsnno', N'partno', N'type', N'budget', N'quantity', N'description', N'createdat')
        SET @SortColumn = N'createdat';

    SELECT
        A.Id,
        A.ProjectId,
        A.PartNo,
        A.Budget,
        A.Description,
        A.DSNNo,
        A.Quantity,
        A.CreatedAt,
        A.ModifiedAt,
        A.ActivityGroupId,
        ActivityGroupName = G.Name,
        TypeId = A.ActivityTypeId,
        [Type] = T.Name,
        QuoteReceived = (
            SELECT CASE
                WHEN ISNULL(A.Quantity, 0) <= 0 THEN 0
                WHEN ISNULL(SUM(CASE WHEN I.UnitPrice IS NOT NULL OR I.TotalPrice IS NOT NULL THEN ISNULL(I.Quantity, 0) ELSE 0 END), 0) >= A.Quantity THEN 100
                ELSE CAST(ROUND(100.0 * ISNULL(SUM(CASE WHEN I.UnitPrice IS NOT NULL OR I.TotalPrice IS NOT NULL THEN ISNULL(I.Quantity, 0) ELSE 0 END), 0) / A.Quantity, 0) AS INT)
            END
            FROM projects.QuotationItems AS I
            INNER JOIN projects.Quotations AS Q
                ON Q.Id = I.QuotationId
               AND Q.IsDeleted = 0
            WHERE I.ActivityId = A.Id
        ),
        DeliveryProgress = (
            SELECT CASE
                WHEN ISNULL(SUM(I.Quantity), 0) <= 0 THEN 0
                ELSE CASE
                    WHEN 100 * SUM(I.AwardedQuantity) / SUM(I.Quantity) > 100 THEN 100
                    ELSE CAST(100 * SUM(I.AwardedQuantity) / SUM(I.Quantity) AS INT)
                END
            END
            FROM projects.QuotationItems AS I
            INNER JOIN projects.Quotations AS Q
                ON Q.Id = I.QuotationId
               AND Q.IsDeleted = 0
            WHERE I.ActivityId = A.Id
        ),
        MainPartNo =
        (
            SELECT TOP (1) S.PartValue
            FROM dbo.SplitAlternativePartNumbers(A.PartNo) AS S
            ORDER BY S.SortOrder
        )
    FROM projects.Activities AS A
    LEFT JOIN projects.ActivityTypes AS T
        ON T.Id = A.ActivityTypeId
    LEFT JOIN projects.ActivityGroups AS G
        ON G.Id = A.ActivityGroupId
    WHERE A.ProjectId = @ProjectId
      AND A.IsDeleted = 0
    ORDER BY
        CASE WHEN @SortDesc = 0 AND @SortColumn = N'dsnno' THEN dbo.NaturalSortKey(A.DSNNo) END ASC,
        CASE WHEN @SortDesc = 1 AND @SortColumn = N'dsnno' THEN dbo.NaturalSortKey(A.DSNNo) END DESC,
        CASE WHEN @SortDesc = 0 AND @SortColumn = N'partno' THEN A.PartNo END ASC,
        CASE WHEN @SortDesc = 1 AND @SortColumn = N'partno' THEN A.PartNo END DESC,
        CASE WHEN @SortDesc = 0 AND @SortColumn = N'type' THEN T.Name END ASC,
        CASE WHEN @SortDesc = 1 AND @SortColumn = N'type' THEN T.Name END DESC,
        CASE WHEN @SortDesc = 0 AND @SortColumn = N'budget' THEN A.Budget END ASC,
        CASE WHEN @SortDesc = 1 AND @SortColumn = N'budget' THEN A.Budget END DESC,
        CASE WHEN @SortDesc = 0 AND @SortColumn = N'quantity' THEN A.Quantity END ASC,
        CASE WHEN @SortDesc = 1 AND @SortColumn = N'quantity' THEN A.Quantity END DESC,
        CASE WHEN @SortDesc = 0 AND @SortColumn = N'description' THEN A.Description END ASC,
        CASE WHEN @SortDesc = 1 AND @SortColumn = N'description' THEN A.Description END DESC,
        CASE WHEN @SortDesc = 0 AND @SortColumn = N'createdat' THEN A.CreatedAt END ASC,
        CASE WHEN @SortDesc = 1 AND @SortColumn = N'createdat' THEN A.CreatedAt END DESC,
        A.Id DESC;

    SELECT
        ActivityId = A.Id,
        AlternativePartNo = S.PartValue,
        S.SortOrder
    FROM projects.Activities AS A
    CROSS APPLY dbo.SplitAlternativePartNumbers(A.PartNo) AS S
    WHERE A.ProjectId = @ProjectId
      AND A.IsDeleted = 0
      AND S.SortOrder > 1
    ORDER BY A.Id, S.SortOrder;
END
