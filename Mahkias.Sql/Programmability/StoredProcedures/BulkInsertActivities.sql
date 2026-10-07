CREATE OR ALTER PROCEDURE [dbo].[BulkInsertActivities]
(
    @ProjectId INT,
    -- TextValue1 = PartNo, TextValue2 = Description, TextValue3 = DSNNo
    -- DecimalValue1 = Budget, NumericValue1 = Quantity, NumericValue2 = TypeId
    @Activities [dbo].[DefaultGenericTableType] READONLY
)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY
        BEGIN TRANSACTION;

    DECLARE @Inserted TABLE
    (
        ActivityId INT NOT NULL,
        ProjectId INT NOT NULL,
        PartNo NVARCHAR(MAX) NULL
    );

    INSERT INTO projects.Activities
    (
        ProjectId,
        PartNo,
        Budget,
        Description,
        DSNNo,
        Quantity,
        ActivityTypeId,
        CreatedAt
    )
    OUTPUT inserted.Id, inserted.ProjectId, inserted.PartNo
        INTO @Inserted (ActivityId, ProjectId, PartNo)
    SELECT
        @ProjectId,
        A.TextValue1,
        CAST(A.DecimalValue1 AS DECIMAL(18, 2)),
        A.TextValue2,
        A.TextValue3,
        A.NumericValue1,
        A.NumericValue2,
        GETDATE()
    FROM @Activities AS A
    WHERE NULLIF(LTRIM(RTRIM(REPLACE(REPLACE(A.TextValue1, CHAR(13), N' '), CHAR(10), N' '))), N'') IS NOT NULL;

    INSERT INTO projects.ActivityAlternativeParts
    (
        ActivityId,
        ProjectId,
        MainPartNo,
        AlternativePartNo,
        SortOrder
    )
    SELECT
        I.ActivityId,
        I.ProjectId,
        LEFT(Main.PartValue, 100),
        LEFT(Alt.PartValue, 100),
        Alt.SortOrder
    FROM @Inserted AS I
    CROSS APPLY dbo.SplitAlternativePartNumbers(I.PartNo) AS Alt
    CROSS APPLY
    (
        SELECT TOP (1) S.PartValue
        FROM dbo.SplitAlternativePartNumbers(I.PartNo) AS S
        ORDER BY S.SortOrder
    ) AS Main
    WHERE Alt.SortOrder > 1;

        COMMIT TRANSACTION;
        SELECT InsertedCount = (SELECT COUNT(*) FROM @Inserted);
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
        BEGIN
            ROLLBACK TRANSACTION;
        END

        ;THROW
    END CATCH
END
