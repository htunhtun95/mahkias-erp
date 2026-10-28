CREATE OR ALTER PROCEDURE [dbo].[BulkInsertActivities]
(
    @ProjectId INT,
    -- TextValue1 = PartNo, TextValue2 = Description, TextValue3 = DSNNo, TextValue4 = Label
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
            PartNo NVARCHAR(MAX) NULL,
            TypeId INT NULL,
            LabelName NVARCHAR(200) NULL
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
                NULLIF(LTRIM(RTRIM(A.TextValue4)), N'') AS LabelName
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
                Source.TypeId,
                GETDATE()
            )
        OUTPUT
            inserted.Id,
            inserted.ProjectId,
            inserted.PartNo,
            inserted.ActivityTypeId,
            Source.LabelName
        INTO @Inserted (ActivityId, ProjectId, PartNo, TypeId, LabelName);

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
