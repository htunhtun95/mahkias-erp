CREATE OR ALTER PROCEDURE [dbo].[ReplaceQuotationItems]
(
    @Id INT,
    @SupplierId INT,
    @SupplierReference NVARCHAR(100) = NULL,
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

        UPDATE projects.Quotations
        SET SupplierId = @SupplierId,
            SupplierReference = NULLIF(LTRIM(RTRIM(@SupplierReference)), N''),
            ModifiedAt = GETDATE()
        WHERE Id = @Id
          AND IsDeleted = 0;

        IF @@ROWCOUNT = 0
        BEGIN
            ROLLBACK TRANSACTION;
            SELECT Updated = 0;
            RETURN;
        END

        DELETE FROM projects.QuotationItems
        WHERE QuotationId = @Id;

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
        SELECT Updated = 1;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
        BEGIN
            ROLLBACK TRANSACTION;
        END

        ;THROW
    END CATCH
END
