CREATE PROCEDURE [dbo].[SaveActivityAlternativeParts]
(
    @ActivityId INT,
    @ProjectId INT,
    @PartNo NVARCHAR(MAX)
)
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE projects.QuotationItems
    SET AlternativePartId = NULL
    WHERE AlternativePartId IN (
        SELECT Id
        FROM projects.ActivityAlternativeParts
        WHERE ActivityId = @ActivityId
    );

    DELETE FROM projects.ActivityAlternativeParts
    WHERE ActivityId = @ActivityId;

    INSERT INTO projects.ActivityAlternativeParts
    (
        ActivityId,
        ProjectId,
        MainPartNo,
        AlternativePartNo,
        SortOrder
    )
    SELECT
        @ActivityId,
        @ProjectId,
        LEFT(Main.PartValue, 100),
        LEFT(Alt.PartValue, 100),
        Alt.SortOrder
    FROM dbo.SplitAlternativePartNumbers(@PartNo) AS Alt
    CROSS APPLY
    (
        SELECT TOP (1) PartValue
        FROM dbo.SplitAlternativePartNumbers(@PartNo)
        ORDER BY SortOrder
    ) AS Main
    WHERE Alt.SortOrder > 1
      AND NULLIF(LEFT(Alt.PartValue, 100), N'') IS NOT NULL;
END
