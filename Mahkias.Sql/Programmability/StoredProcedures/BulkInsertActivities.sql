CREATE PROCEDURE [dbo].[BulkInsertActivities]
(
    @ProjectId INT,
    -- TextValue1 = PartNo, TextValue2 = Description, TextValue3 = DSNNo, TextValue4 = Label
    -- TextValue5 = Condition, TextValue6 = Unit
    -- TextValue7 = JSON {"SerialNo","TSN","TSO","CSN","CSO"}
    -- DecimalValue1 = Budget, NumericValue1 = Quantity, NumericValue2 = TypeId
    -- NumericValue3 = selected PartId. Null creates a new Part when @LinkParts = 1.
    @Activities [dbo].[DefaultGenericTableType] READONLY,
    @LinkParts BIT = 0
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
            PartNo NVARCHAR(MAX) NULL,
            TypeId INT NULL,
            LabelName NVARCHAR(200) NULL,
            Description NVARCHAR(MAX) NULL,
            RequestedPartId INT NULL
        );

        MERGE projects.Activities AS Target
        USING
        (
            SELECT
                @ProjectId AS ProjectId,
                A.TextValue1 AS PartNo,
                CAST(A.DecimalValue1 AS DECIMAL(18, 2)) AS Budget,
                A.TextValue2 AS Description,
                A.TextValue3 AS DSNNo,
                A.NumericValue1 AS Quantity,
                A.NumericValue2 AS TypeId,
                NULLIF(LTRIM(RTRIM(A.TextValue4)), N'') AS LabelName,
                LEFT(NULLIF(LTRIM(RTRIM(A.TextValue5)), N''), 100) AS Condition,
                LEFT(NULLIF(LTRIM(RTRIM(A.TextValue6)), N''), 100) AS Unit,
                LEFT(NULLIF(LTRIM(RTRIM(JSON_VALUE(A.TextValue7, '$.SerialNo'))), N''), 100) AS SerialNo,
                LEFT(NULLIF(LTRIM(RTRIM(JSON_VALUE(A.TextValue7, '$.TSN'))), N''), 100) AS TSN,
                LEFT(NULLIF(LTRIM(RTRIM(JSON_VALUE(A.TextValue7, '$.TSO'))), N''), 100) AS TSO,
                LEFT(NULLIF(LTRIM(RTRIM(JSON_VALUE(A.TextValue7, '$.CSN'))), N''), 100) AS CSN,
                LEFT(NULLIF(LTRIM(RTRIM(JSON_VALUE(A.TextValue7, '$.CSO'))), N''), 100) AS CSO,
                A.TextValue2 AS RawDescription,
                NULLIF(A.NumericValue3, 0) AS RequestedPartId
            FROM @Activities AS A
            WHERE NULLIF(LTRIM(RTRIM(REPLACE(REPLACE(A.TextValue1, CHAR(13), N' '), CHAR(10), N' '))), N'') IS NOT NULL
        ) AS Source
        ON 1 = 0
        WHEN NOT MATCHED THEN
            INSERT
            (
                ProjectId,
                PartNo,
                Budget,
                Description,
                DSNNo,
                Quantity,
                Condition,
                Unit,
                SerialNo,
                TSN,
                TSO,
                CSN,
                CSO,
                ActivityTypeId,
                CreatedAt
            )
            VALUES
            (
                Source.ProjectId,
                Source.PartNo,
                Source.Budget,
                Source.Description,
                Source.DSNNo,
                Source.Quantity,
                Source.Condition,
                Source.Unit,
                Source.SerialNo,
                Source.TSN,
                Source.TSO,
                Source.CSN,
                Source.CSO,
                Source.TypeId,
                GETDATE()
            )
        OUTPUT
            inserted.Id,
            inserted.ProjectId,
            inserted.PartNo,
            inserted.ActivityTypeId,
            Source.LabelName,
            Source.RawDescription,
            Source.RequestedPartId
        INTO @Inserted (ActivityId, ProjectId, PartNo, TypeId, LabelName, Description, RequestedPartId);

        IF @LinkParts = 1
        BEGIN
            DECLARE @ActivityId INT;
            DECLARE @RowPartNo NVARCHAR(MAX);
            DECLARE @RowDescription NVARCHAR(MAX);
            DECLARE @RequestedPartId INT;
            DECLARE @ResolvedPartId INT;
            DECLARE part_rows CURSOR LOCAL FAST_FORWARD FOR
                SELECT ActivityId, PartNo, Description, RequestedPartId
                FROM @Inserted;
            OPEN part_rows;
            FETCH NEXT FROM part_rows INTO @ActivityId, @RowPartNo, @RowDescription, @RequestedPartId;
            WHILE @@FETCH_STATUS = 0
            BEGIN
                SET @ResolvedPartId = NULL;
                EXEC dbo.ResolveActivityPart
                    @PartId = @RequestedPartId,
                    @PartNo = @RowPartNo,
                    @Description = @RowDescription,
                    @ResolvedPartId = @ResolvedPartId OUTPUT;
                UPDATE projects.Activities
                SET PartId = @ResolvedPartId
                WHERE Id = @ActivityId;
                FETCH NEXT FROM part_rows INTO @ActivityId, @RowPartNo, @RowDescription, @RequestedPartId;
            END
            CLOSE part_rows;
            DEALLOCATE part_rows;
        END

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

        INSERT INTO projects.ActivityLabels (ProjectId, Name, ActivityTypeId)
        SELECT @ProjectId, Picked.LabelName, Keys.TypeId
        FROM
        (
            SELECT DISTINCT I.TypeId, LOWER(LTRIM(RTRIM(I.LabelName))) AS NameKey
            FROM @Inserted AS I
            WHERE I.TypeId IS NOT NULL
              AND NULLIF(LTRIM(RTRIM(I.LabelName)), N'') IS NOT NULL
        ) AS Keys
        CROSS APPLY
        (
            SELECT TOP (1) LTRIM(RTRIM(I.LabelName)) AS LabelName
            FROM @Inserted AS I
            WHERE I.TypeId = Keys.TypeId
              AND LOWER(LTRIM(RTRIM(I.LabelName))) = Keys.NameKey
            ORDER BY I.ActivityId
        ) AS Picked
        WHERE NOT EXISTS
        (
            SELECT 1
            FROM projects.ActivityLabels AS AL
            WHERE AL.ProjectId = @ProjectId
              AND AL.ActivityTypeId = Keys.TypeId
              AND LOWER(AL.Name) = Keys.NameKey
        );

        INSERT INTO projects.ActivityLabelActivities (ActivityLabelId, ActivityId)
        SELECT AL.Id, I.ActivityId
        FROM @Inserted AS I
        INNER JOIN projects.ActivityLabels AS AL
            ON AL.ProjectId = @ProjectId
           AND AL.ActivityTypeId = I.TypeId
           AND LOWER(AL.Name) = LOWER(LTRIM(RTRIM(I.LabelName)))
        WHERE I.TypeId IS NOT NULL
          AND NULLIF(LTRIM(RTRIM(I.LabelName)), N'') IS NOT NULL
          AND NOT EXISTS
          (
              SELECT 1
              FROM projects.ActivityLabelActivities AS Existing
              WHERE Existing.ActivityLabelId = AL.Id
                AND Existing.ActivityId = I.ActivityId
          );

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
