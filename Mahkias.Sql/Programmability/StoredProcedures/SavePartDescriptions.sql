CREATE PROCEDURE [dbo].[SavePartDescriptions]
(
    @PartId INT,
    @Descriptions NVARCHAR(MAX) = NULL,
    @Replace BIT = 0,
    @Error NVARCHAR(400) OUTPUT
)
AS
BEGIN
    SET NOCOUNT ON;
    SET @Error = NULL;

    DECLARE @Incoming TABLE
    (
        Id INT NULL,
        Description NVARCHAR(MAX) NOT NULL,
        DescriptionKey VARBINARY(32) NOT NULL
    );

    INSERT INTO @Incoming (Id, Description, DescriptionKey)
    SELECT
        CASE WHEN @Replace = 1 THEN NULLIF(J.Id, 0) ELSE NULL END,
        LTRIM(RTRIM(J.Description)),
        HASHBYTES('SHA2_256', LOWER(LTRIM(RTRIM(CAST(J.Description AS NVARCHAR(4000))))))
    FROM OPENJSON(ISNULL(@Descriptions, N'[]'))
    WITH
    (
        Id INT '$.id',
        Description NVARCHAR(MAX) '$.description'
    ) AS J
    WHERE NULLIF(LTRIM(RTRIM(J.Description)), N'') IS NOT NULL;

    IF EXISTS
    (
        SELECT DescriptionKey
        FROM @Incoming
        GROUP BY DescriptionKey
        HAVING COUNT(*) > 1
    )
    BEGIN
        SET @Error = N'Each description can only be added once.';
        RETURN;
    END

    IF @Replace = 1
    BEGIN
        DELETE D
        FROM projects.PartDescriptions AS D
        WHERE D.PartId = @PartId
          AND NOT EXISTS
          (
              SELECT 1
              FROM @Incoming AS I
              WHERE I.Id = D.Id
          );

        UPDATE D
        SET Description = I.Description
        FROM projects.PartDescriptions AS D
        INNER JOIN @Incoming AS I
            ON I.Id = D.Id
        WHERE D.PartId = @PartId
          AND ISNULL(D.Description, N'') <> I.Description;
    END

    INSERT INTO projects.PartDescriptions (PartId, Description)
    SELECT @PartId, I.Description
    FROM @Incoming AS I
    WHERE
    (
        @Replace = 0
        OR I.Id IS NULL
        OR NOT EXISTS
        (
            SELECT 1
            FROM projects.PartDescriptions AS D
            WHERE D.Id = I.Id
              AND D.PartId = @PartId
        )
    )
    AND NOT EXISTS
    (
        SELECT 1
        FROM projects.PartDescriptions AS D
        WHERE D.PartId = @PartId
          AND LOWER(LTRIM(RTRIM(D.Description))) = LOWER(I.Description)
    );
END
