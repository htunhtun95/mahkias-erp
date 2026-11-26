CREATE PROCEDURE [dbo].[UpdatePart]
(
    @Id INT,
    @PartNo NVARCHAR(MAX),
    @Descriptions NVARCHAR(MAX) = NULL
)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF NOT EXISTS (SELECT 1 FROM projects.Parts WHERE Id = @Id)
    BEGIN
        SELECT
            Id = CAST(NULL AS INT),
            PartNo = CAST(NULL AS NVARCHAR(MAX)),
            CreatedAt = CAST(NULL AS DATETIME2),
            Descriptions = CAST(NULL AS NVARCHAR(MAX)),
            Message = N'Part was not found.';
        RETURN;
    END

    DECLARE @Number NVARCHAR(MAX) = NULLIF(LTRIM(RTRIM(@PartNo)), N'');
    IF @Number IS NULL
    BEGIN
        SELECT
            Id = CAST(NULL AS INT),
            PartNo = CAST(NULL AS NVARCHAR(MAX)),
            CreatedAt = CAST(NULL AS DATETIME2),
            Descriptions = CAST(NULL AS NVARCHAR(MAX)),
            Message = N'Part number is required.';
        RETURN;
    END

    BEGIN TRY
        BEGIN TRANSACTION;

        DECLARE @Pieces TABLE
        (
            SortOrder INT NOT NULL PRIMARY KEY,
            PartValue NVARCHAR(MAX) NOT NULL
        );

        INSERT INTO @Pieces (SortOrder, PartValue)
        SELECT SortOrder, PartValue
        FROM dbo.SplitOrPartNumbers(@Number);

        DECLARE @Error NVARCHAR(400);
        DECLARE @PieceCount INT = (SELECT COUNT(*) FROM @Pieces);
        DECLARE @FirstPartNo NVARCHAR(MAX) = CASE
            WHEN @PieceCount > 1 THEN (SELECT TOP (1) PartValue FROM @Pieces ORDER BY SortOrder)
            ELSE @Number
        END;

        UPDATE projects.Parts
        SET PartNo = @FirstPartNo
        WHERE Id = @Id;

        EXEC dbo.SavePartDescriptions
            @PartId = @Id,
            @Descriptions = @Descriptions,
            @Replace = 1,
            @Error = @Error OUTPUT;

        IF @Error IS NULL AND @PieceCount > 1
        BEGIN
            DECLARE @SortOrder INT;
            DECLARE @PartValue NVARCHAR(MAX);
            DECLARE @NewId INT;

            DELETE FROM @Pieces
            WHERE SortOrder = (SELECT MIN(SortOrder) FROM @Pieces);

            WHILE EXISTS (SELECT 1 FROM @Pieces)
            BEGIN
                SELECT TOP (1)
                    @SortOrder = SortOrder,
                    @PartValue = PartValue
                FROM @Pieces
                ORDER BY SortOrder;

                DELETE FROM @Pieces
                WHERE SortOrder = @SortOrder;

                INSERT INTO projects.Parts (PartNo)
                VALUES (@PartValue);

                SET @NewId = CAST(SCOPE_IDENTITY() AS INT);
                EXEC dbo.SavePartDescriptions
                    @PartId = @NewId,
                    @Descriptions = @Descriptions,
                    @Replace = 0,
                    @Error = @Error OUTPUT;

                IF @Error IS NOT NULL
                BEGIN
                    BREAK;
                END
            END
        END

        IF @Error IS NOT NULL
        BEGIN
            ROLLBACK TRANSACTION;
            SELECT
                Id = CAST(NULL AS INT),
                PartNo = CAST(NULL AS NVARCHAR(MAX)),
                CreatedAt = CAST(NULL AS DATETIME2),
                Descriptions = CAST(NULL AS NVARCHAR(MAX)),
                Message = @Error;
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

    EXEC dbo.GetPart @Id = @Id;
END
