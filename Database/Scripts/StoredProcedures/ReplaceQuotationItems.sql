CREATE OR ALTER PROCEDURE [dbo].[ReplaceQuotationItems]
(
    @Id INT,
    @SupplierId INT,
    @SupplierReference NVARCHAR(100) = NULL,
    @CurrencyId INT = NULL,
    -- NumericValue1 = ActivityId, NumericValue2 = ActivityGroupId, NumericValue3 = AlternativePartId, NumericValue4 = Quantity, NumericValue5 = CurrencyId
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
        DECLARE @OwnTransaction BIT = 0;
        IF @@TRANCOUNT = 0
        BEGIN
            BEGIN TRANSACTION;
            SET @OwnTransaction = 1;
        END

        UPDATE projects.Quotations
        SET SupplierId = @SupplierId,
            SupplierReference = NULLIF(LTRIM(RTRIM(@SupplierReference)), N''),
            ModifiedAt = GETDATE()
        WHERE Id = @Id
          AND IsDeleted = 0;

        IF @@ROWCOUNT = 0
        BEGIN
            IF @OwnTransaction = 1
            BEGIN
                ROLLBACK TRANSACTION;
            END

            SELECT Updated = 0;
            RETURN;
        END

        DELETE FROM projects.QuotationItems
        WHERE QuotationId = @Id;

        DECLARE @BaseCurrencyId INT;
        SELECT @BaseCurrencyId = Id
        FROM dbo.Currencies
        WHERE IsDefault = 1;

        IF @BaseCurrencyId IS NULL
        BEGIN
            ;THROW 50001, N'No default currency is configured.', 1;
        END

        DECLARE @ResolvedCurrencyId INT = @CurrencyId;
        IF @ResolvedCurrencyId IS NULL
           OR NOT EXISTS (
                SELECT 1
                FROM dbo.Currencies
                WHERE Id = @ResolvedCurrencyId
                  AND IsActive = 1
           )
        BEGIN
            SET @ResolvedCurrencyId = @BaseCurrencyId;
        END

        DECLARE @ExchangeRate DECIMAL(18, 6) = 1.0;
        IF @ResolvedCurrencyId <> @BaseCurrencyId
        BEGIN
            SELECT TOP (1) @ExchangeRate = Rate
            FROM dbo.ExchangeRates
            WHERE FromCurrencyId = @ResolvedCurrencyId
              AND ToCurrencyId = @BaseCurrencyId
              AND EffectiveDate <= CONVERT(DATE, GETDATE())
            ORDER BY EffectiveDate DESC, ExchangeRateId DESC;
        END

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
            CurrencyId,
            ExchangeRate,
            UnitPrice,
            BaseUnitPrice,
            Quantity,
            TotalPrice,
            BaseTotalPrice,
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
            Resolved.CurrencyId,
            Resolved.Rate,
            CAST(I.DecimalValue1 AS DECIMAL(18, 2)),
            CAST(ROUND(I.DecimalValue1 * Resolved.Rate, 2) AS DECIMAL(18, 2)),
            ISNULL(I.NumericValue4, 1),
            CAST(I.DecimalValue2 AS DECIMAL(18, 2)),
            CAST(ROUND(I.DecimalValue2 * Resolved.Rate, 2) AS DECIMAL(18, 2)),
            0
        FROM @Items AS I
        CROSS APPLY (
            SELECT
                CurrencyId = COALESCE(Active.Id, @ResolvedCurrencyId, @BaseCurrencyId),
                Rate = CASE
                    WHEN COALESCE(Active.Id, @ResolvedCurrencyId, @BaseCurrencyId) = @BaseCurrencyId THEN CAST(1.0 AS DECIMAL(18, 6))
                    ELSE ISNULL((
                        SELECT TOP (1) Rate
                        FROM dbo.ExchangeRates
                        WHERE FromCurrencyId = COALESCE(Active.Id, @ResolvedCurrencyId, @BaseCurrencyId)
                          AND ToCurrencyId = @BaseCurrencyId
                          AND EffectiveDate <= CONVERT(DATE, GETDATE())
                        ORDER BY EffectiveDate DESC, ExchangeRateId DESC
                    ), CAST(1.0 AS DECIMAL(18, 6)))
                END
            FROM (SELECT 1 AS Anchor) AS Anchor
            LEFT JOIN dbo.Currencies AS Active
                ON Active.Id = NULLIF(I.NumericValue5, 0)
               AND Active.IsActive = 1
        ) AS Resolved;

        UPDATE projects.Quotations
        SET CurrencyId = @ResolvedCurrencyId,
            ExchangeRate = @ExchangeRate,
            BaseTotalAmount = ROUND(ISNULL((
                SELECT SUM(BaseTotalPrice)
                FROM projects.QuotationItems
                WHERE QuotationId = @Id
            ), 0), 2)
        WHERE Id = @Id;

        IF @OwnTransaction = 1
        BEGIN
            COMMIT TRANSACTION;
        END

        SELECT Updated = 1;
    END TRY
    BEGIN CATCH
        IF @OwnTransaction = 1 AND @@TRANCOUNT > 0
        BEGIN
            ROLLBACK TRANSACTION;
        END

        ;THROW
    END CATCH
END
GO
