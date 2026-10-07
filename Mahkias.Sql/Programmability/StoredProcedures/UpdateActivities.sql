CREATE OR ALTER PROCEDURE [dbo].[UpdateActivities]
(
    -- NumericValue1 = Id, NumericValue2 = ProjectId, NumericValue3 = Quantity, NumericValue4 = TypeId
    -- TextValue1 = PartNo, TextValue2 = Description, TextValue3 = DSNNo, DecimalValue1 = Budget
    @Activities [dbo].[DefaultGenericTableType] READONLY
)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @Requested INT = (SELECT COUNT(*) FROM @Activities);
    IF @Requested = 0
    BEGIN
        SELECT Updated = 0;
        RETURN;
    END

    IF EXISTS (
        SELECT NumericValue1
        FROM @Activities
        GROUP BY NumericValue1
        HAVING COUNT(*) > 1
    )
    BEGIN
        SELECT Updated = 0;
        RETURN;
    END

    IF EXISTS (
        SELECT 1
        FROM @Activities AS A
        WHERE A.NumericValue1 IS NULL
           OR NULLIF(LTRIM(RTRIM(REPLACE(REPLACE(A.TextValue1, CHAR(13), N' '), CHAR(10), N' '))), N'') IS NULL
           OR NULLIF(LTRIM(RTRIM(A.TextValue3)), N'') IS NULL
           OR NULLIF(A.NumericValue4, 0) IS NULL
    )
    BEGIN
        SELECT Updated = 0;
        RETURN;
    END

    BEGIN TRY
        BEGIN TRANSACTION;

        DECLARE @Updated TABLE
        (
            ActivityId INT NOT NULL,
            ProjectId INT NOT NULL,
            PartNo NVARCHAR(MAX) NULL
        );

        UPDATE T
        SET
            DSNNo = LTRIM(RTRIM(S.TextValue3)),
            PartNo = S.TextValue1,
            ActivityTypeId = S.NumericValue4,
            Quantity = S.NumericValue3,
            Budget = CAST(S.DecimalValue1 AS DECIMAL(18, 2)),
            Description = NULLIF(S.TextValue2, N''),
            ModifiedAt = GETDATE()
        OUTPUT inserted.Id, inserted.ProjectId, inserted.PartNo
            INTO @Updated (ActivityId, ProjectId, PartNo)
        FROM projects.Activities AS T
        INNER JOIN @Activities AS S
            ON S.NumericValue1 = T.Id
        WHERE T.IsDeleted = 0
          AND (S.NumericValue2 IS NULL OR T.ProjectId = S.NumericValue2);

        IF (SELECT COUNT(*) FROM @Updated) <> @Requested
        BEGIN
            ROLLBACK TRANSACTION;
            SELECT Updated = 0;
            RETURN;
        END

        UPDATE QI
        SET AlternativePartId = NULL
        FROM projects.QuotationItems AS QI
        INNER JOIN projects.ActivityAlternativeParts AS AP
            ON AP.Id = QI.AlternativePartId
        INNER JOIN @Updated AS U
            ON U.ActivityId = AP.ActivityId;

        DELETE AP
        FROM projects.ActivityAlternativeParts AS AP
        INNER JOIN @Updated AS U
            ON U.ActivityId = AP.ActivityId;

        INSERT INTO projects.ActivityAlternativeParts
        (
            ActivityId,
            ProjectId,
            MainPartNo,
            AlternativePartNo,
            SortOrder
        )
        SELECT
            U.ActivityId,
            U.ProjectId,
            LEFT(Main.PartValue, 100),
            LEFT(Alt.PartValue, 100),
            Alt.SortOrder
        FROM @Updated AS U
        CROSS APPLY dbo.SplitAlternativePartNumbers(U.PartNo) AS Alt
        CROSS APPLY
        (
            SELECT TOP (1) S.PartValue
            FROM dbo.SplitAlternativePartNumbers(U.PartNo) AS S
            ORDER BY S.SortOrder
        ) AS Main
        WHERE Alt.SortOrder > 1
          AND NULLIF(LEFT(Alt.PartValue, 100), N'') IS NOT NULL;

        COMMIT TRANSACTION;
        SELECT Updated = (SELECT COUNT(*) FROM @Updated);
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
        BEGIN
            ROLLBACK TRANSACTION;
        END

        ;THROW
    END CATCH
END
