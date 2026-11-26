CREATE PROCEDURE [dbo].[ResolveActivityPart]
(
    @PartId INT = NULL,
    @PartNo NVARCHAR(MAX),
    @Description NVARCHAR(MAX) = NULL,
    @ResolvedPartId INT OUTPUT
)
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Number NVARCHAR(MAX) = NULLIF(LTRIM(RTRIM(@PartNo)), N'');
    IF @Number IS NULL
    BEGIN
        SET @ResolvedPartId = NULL;
        RETURN;
    END

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
        SET @ResolvedPartId = NULL;
        RETURN;
    END

    DECLARE @Main NVARCHAR(MAX) = (SELECT TOP (1) PartValue FROM @Pieces ORDER BY SortOrder);
    DECLARE @UseSelected BIT = CASE
        WHEN @PartId IS NOT NULL
         AND EXISTS
         (
             SELECT 1
             FROM projects.Parts
             WHERE Id = @PartId
               AND LOWER(LTRIM(RTRIM(PartNo))) = LOWER(LTRIM(RTRIM(@Main)))
         ) THEN 1
        ELSE 0
    END;
    DECLARE @SortOrder INT;
    DECLARE @PartValue NVARCHAR(MAX);
    DECLARE @TargetId INT;

    IF @UseSelected = 1
    BEGIN
        SET @ResolvedPartId = @PartId;
        SET @TargetId = @PartId;
    END
    ELSE
    BEGIN
        SET @ResolvedPartId = NULL;
        SET @TargetId = NULL;
    END

    WHILE EXISTS (SELECT 1 FROM @Pieces)
    BEGIN
        SELECT TOP (1)
            @SortOrder = SortOrder,
            @PartValue = PartValue
        FROM @Pieces
        ORDER BY SortOrder;

        DELETE FROM @Pieces WHERE SortOrder = @SortOrder;

        IF @UseSelected = 1 AND LOWER(LTRIM(RTRIM(@PartValue))) = LOWER(LTRIM(RTRIM(@Main)))
        BEGIN
            SET @TargetId = @PartId;
        END
        ELSE IF @UseSelected = 1
        BEGIN
            SET @TargetId = NULL;
            SELECT TOP (1) @TargetId = P.Id
            FROM projects.Parts AS P
            WHERE LOWER(LTRIM(RTRIM(P.PartNo))) = LOWER(LTRIM(RTRIM(@PartValue)))
              AND (SELECT COUNT(*) FROM dbo.SplitOrPartNumbers(P.PartNo)) = 1
            ORDER BY P.Id;

            IF @TargetId IS NULL
            BEGIN
                INSERT INTO projects.Parts (PartNo)
                VALUES (@PartValue);

                SET @TargetId = CAST(SCOPE_IDENTITY() AS INT);
            END
        END
        ELSE
        BEGIN
            INSERT INTO projects.Parts (PartNo)
            VALUES (@PartValue);

            SET @TargetId = CAST(SCOPE_IDENTITY() AS INT);
            IF @ResolvedPartId IS NULL
            BEGIN
                SET @ResolvedPartId = @TargetId;
            END
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
        END
    END
END
