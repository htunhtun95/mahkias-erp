CREATE OR ALTER PROCEDURE [dbo].[CreateQuotation]
(
    @SupplierId INT,
    @SupplierReference NVARCHAR(100) = NULL,
    @DriveFileLink NVARCHAR(1000) = NULL,
    @ProjectIds [dbo].[NumberTableType] READONLY,
    -- NumericValue1 = ActivityId, NumericValue2 = ActivityGroupId, NumericValue3 = AlternativePartId, NumericValue4 = Quantity
    -- BitValue1 = IsAlternativePart
    -- TextValue1 = DsnNo, TextValue2 = PartNo, TextValue3 = PartDescription
    -- DecimalValue1 = UnitPrice, DecimalValue2 = TotalPrice
    @Items [dbo].[DefaultGenericTableType] READONLY
)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY
        BEGIN TRANSACTION;

        DECLARE @Id INT;

        INSERT INTO projects.Quotations (SupplierId, Code, SupplierReference, DriveFileLink)
        VALUES (
            @SupplierId,
            N'T' + REPLACE(CONVERT(NVARCHAR(36), NEWID()), N'-', N''),
            NULLIF(LTRIM(RTRIM(@SupplierReference)), N''),
            NULLIF(LTRIM(RTRIM(@DriveFileLink)), N'')
        );

        SET @Id = CAST(SCOPE_IDENTITY() AS INT);

        UPDATE projects.Quotations
        SET Code = CASE
                WHEN @Id <= 99999 THEN N'Q-' + RIGHT(N'00000' + CAST(@Id AS NVARCHAR(10)), 5)
                ELSE N'Q-' + CAST(@Id AS NVARCHAR(20))
            END
        WHERE Id = @Id;

        INSERT INTO projects.QuotationProjects (QuotationId, ProjectId)
        SELECT DISTINCT @Id, P.NumericID
        FROM @ProjectIds AS P
        WHERE P.NumericID > 0;

        INSERT INTO projects.QuotationItems
        (
            QuotationId,
            ActivityId,
            ActivityGroupId,
            IsAlternativePart,
            AlternativePartId,
            DsnNo,
            PartNo,
            PartDescription,
            UnitPrice,
            Quantity,
            TotalPrice,
            AwardedQuantity
        )
        SELECT
            @Id,
            NULLIF(I.NumericValue1, 0),
            NULLIF(I.NumericValue2, 0),
            ISNULL(I.BitValue1, 0),
            NULLIF(I.NumericValue3, 0),
            LEFT(NULLIF(LTRIM(RTRIM(I.TextValue1)), N''), 100),
            LEFT(NULLIF(LTRIM(RTRIM(I.TextValue2)), N''), 100),
            LEFT(NULLIF(LTRIM(RTRIM(I.TextValue3)), N''), 500),
            CAST(I.DecimalValue1 AS DECIMAL(18, 4)),
            ISNULL(I.NumericValue4, 1),
            CAST(I.DecimalValue2 AS DECIMAL(18, 4)),
            0
        FROM @Items AS I;

        COMMIT TRANSACTION;
        SELECT Id = @Id;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
        BEGIN
            ROLLBACK TRANSACTION;
        END

        ;THROW
    END CATCH
END
