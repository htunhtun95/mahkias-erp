CREATE PROCEDURE [dbo].[SavePartAlternate]
(
    @PartId INT,
    @LinkId INT = NULL,
    @PartNo NVARCHAR(MAX),
    @Description NVARCHAR(MAX) = NULL,
    @SelectedPartId INT = NULL
)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF NOT EXISTS (SELECT 1 FROM projects.Parts WHERE Id = @PartId)
    BEGIN
        SELECT
            Id = CAST(NULL AS INT),
            PartId = CAST(NULL AS INT),
            PartNo = CAST(NULL AS NVARCHAR(MAX)),
            Descriptions = CAST(NULL AS NVARCHAR(MAX)),
            Message = N'Part was not found.';
        RETURN;
    END

    IF @LinkId IS NOT NULL
       AND NOT EXISTS (SELECT 1 FROM projects.PartAlternates WHERE Id = @LinkId AND PartId = @PartId)
    BEGIN
        SELECT
            Id = CAST(NULL AS INT),
            PartId = CAST(NULL AS INT),
            PartNo = CAST(NULL AS NVARCHAR(MAX)),
            Descriptions = CAST(NULL AS NVARCHAR(MAX)),
            Message = N'Alternate part was not found.';
        RETURN;
    END

    DECLARE @Number NVARCHAR(MAX) = NULLIF(LTRIM(RTRIM(@PartNo)), N'');
    IF @Number IS NULL
    BEGIN
        SELECT
            Id = CAST(NULL AS INT),
            PartId = CAST(NULL AS INT),
            PartNo = CAST(NULL AS NVARCHAR(MAX)),
            Descriptions = CAST(NULL AS NVARCHAR(MAX)),
            Message = N'Part number is required.';
        RETURN;
    END

    DECLARE @MainNo NVARCHAR(MAX) = (SELECT PartNo FROM projects.Parts WHERE Id = @PartId);
    DECLARE @Text NVARCHAR(MAX) = NULLIF(LTRIM(RTRIM(@Description)), N'');
    DECLARE @Pieces TABLE
    (
        SortOrder INT NOT NULL PRIMARY KEY,
        PartValue NVARCHAR(MAX) NOT NULL
    );

    INSERT INTO @Pieces (SortOrder, PartValue)
    SELECT SortOrder, PartValue
    FROM dbo.SplitOrPartNumbers(@Number);

    IF NOT EXISTS (SELECT 1 FROM @Pieces)
    BEGIN
        SELECT
            Id = CAST(NULL AS INT),
            PartId = CAST(NULL AS INT),
            PartNo = CAST(NULL AS NVARCHAR(MAX)),
            Descriptions = CAST(NULL AS NVARCHAR(MAX)),
            Message = N'Part number is required.';
        RETURN;
    END

    DECLARE @Targets TABLE
    (
        PartId INT NOT NULL PRIMARY KEY
    );

    DECLARE @SortOrder INT;
    DECLARE @PartValue NVARCHAR(MAX);
    DECLARE @TargetId INT;
    DECLARE @AddedDescription BIT = 0;
    DECLARE @AddedLink INT = 0;

    BEGIN TRY
        BEGIN TRANSACTION;

        WHILE EXISTS (SELECT 1 FROM @Pieces)
        BEGIN
        SELECT TOP (1) @SortOrder = SortOrder, @PartValue = PartValue
        FROM @Pieces
        ORDER BY SortOrder;

        DELETE FROM @Pieces WHERE SortOrder = @SortOrder;

        IF LOWER(LTRIM(RTRIM(@PartValue))) = LOWER(LTRIM(RTRIM(@MainNo)))
        BEGIN
            CONTINUE;
        END

        SET @TargetId = NULL;

        IF @SelectedPartId IS NOT NULL
           AND EXISTS
           (
               SELECT 1
               FROM projects.Parts
               WHERE Id = @SelectedPartId
                 AND Id <> @PartId
                 AND LOWER(LTRIM(RTRIM(PartNo))) = LOWER(LTRIM(RTRIM(@PartValue)))
           )
        BEGIN
            SET @TargetId = @SelectedPartId;
        END
        ELSE
        BEGIN
            SELECT TOP (1) @TargetId = P.Id
            FROM projects.Parts AS P
            WHERE P.Id <> @PartId
              AND LOWER(LTRIM(RTRIM(P.PartNo))) = LOWER(LTRIM(RTRIM(@PartValue)))
              AND (SELECT COUNT(*) FROM dbo.SplitOrPartNumbers(P.PartNo)) = 1
            ORDER BY P.Id;
        END

        IF @TargetId IS NULL
        BEGIN
            INSERT INTO projects.Parts (PartNo)
            VALUES (@PartValue);

            SET @TargetId = CAST(SCOPE_IDENTITY() AS INT);
        END

        IF @Text IS NOT NULL
           AND NOT EXISTS
           (
               SELECT 1
               FROM projects.PartDescriptions
               WHERE PartId = @TargetId
                 AND LOWER(LTRIM(RTRIM(Description))) = LOWER(@Text)
           )
        BEGIN
            INSERT INTO projects.PartDescriptions (PartId, Description)
            VALUES (@TargetId, @Text);

            SET @AddedDescription = 1;
        END

        IF NOT EXISTS (SELECT 1 FROM @Targets WHERE PartId = @TargetId)
        BEGIN
            INSERT INTO @Targets (PartId) VALUES (@TargetId);
        END
        END

        IF NOT EXISTS (SELECT 1 FROM @Targets)
        BEGIN
            ROLLBACK TRANSACTION;
            SELECT
                Id = CAST(NULL AS INT),
                PartId = CAST(NULL AS INT),
                PartNo = CAST(NULL AS NVARCHAR(MAX)),
                Descriptions = CAST(NULL AS NVARCHAR(MAX)),
                Message = N'Enter a different part number. A part cannot be an alternate of itself.';
            RETURN;
        END

        IF @LinkId IS NOT NULL
           AND NOT EXISTS
           (
               SELECT 1
               FROM projects.PartAlternates AS L
               INNER JOIN @Targets AS T
                   ON T.PartId = L.AlternatePartId
               WHERE L.Id = @LinkId
                 AND L.PartId = @PartId
           )
        BEGIN
            DELETE FROM projects.PartAlternates
            WHERE Id = @LinkId
              AND PartId = @PartId;
        END

        INSERT INTO projects.PartAlternates (PartId, AlternatePartId)
        SELECT @PartId, T.PartId
        FROM @Targets AS T
        WHERE NOT EXISTS
        (
            SELECT 1
            FROM projects.PartAlternates AS L
            WHERE L.PartId = @PartId
              AND L.AlternatePartId = T.PartId
        );

        SET @AddedLink = @@ROWCOUNT;

        IF @LinkId IS NULL AND @AddedLink = 0 AND @AddedDescription = 0
        BEGIN
            ROLLBACK TRANSACTION;
            SELECT
                Id = CAST(NULL AS INT),
                PartId = CAST(NULL AS INT),
                PartNo = CAST(NULL AS NVARCHAR(MAX)),
                Descriptions = CAST(NULL AS NVARCHAR(MAX)),
                Message = N'That part is already an alternate.';
            RETURN;
        END

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
        BEGIN
            ROLLBACK TRANSACTION;
        END
        ;THROW
    END CATCH

    EXEC dbo.ListPartAlternates @PartId = @PartId;
END
