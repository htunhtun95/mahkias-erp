CREATE PROCEDURE [dbo].[CreatePart]
(
    @PartNo NVARCHAR(MAX),
    @Descriptions NVARCHAR(MAX) = NULL
)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

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

        DECLARE @Id INT = NULL;
        DECLARE @Error NVARCHAR(400);
        DECLARE @SortOrder INT;
        DECLARE @PartValue NVARCHAR(MAX);
        DECLARE @NewId INT;

        IF (SELECT COUNT(*) FROM @Pieces) > 1
        BEGIN
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
                IF @Id IS NULL
                BEGIN
                    SET @Id = @NewId;
                END

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
        ELSE
        BEGIN
            INSERT INTO projects.Parts (PartNo)
            VALUES (@Number);

            SET @Id = CAST(SCOPE_IDENTITY() AS INT);
            EXEC dbo.SavePartDescriptions
                @PartId = @Id,
                @Descriptions = @Descriptions,
                @Replace = 0,
                @Error = @Error OUTPUT;
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
