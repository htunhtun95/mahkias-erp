CREATE FUNCTION [dbo].[NaturalSortKey]
(
    @Value NVARCHAR(400)
)
RETURNS NVARCHAR(2000)
AS
BEGIN
    IF @Value IS NULL
        RETURN NULL;

    DECLARE @Result NVARCHAR(2000) = N'';
    DECLARE @Index INT = 1;
    DECLARE @Length INT = LEN(@Value);
    DECLARE @IsDigit BIT;
    DECLARE @Chunk NVARCHAR(400);

    WHILE @Index <= @Length
    BEGIN
        SET @IsDigit = CASE WHEN SUBSTRING(@Value, @Index, 1) LIKE N'[0-9]' THEN 1 ELSE 0 END;
        SET @Chunk = N'';

        WHILE @Index <= @Length
          AND (
                (@IsDigit = 1 AND SUBSTRING(@Value, @Index, 1) LIKE N'[0-9]')
             OR (@IsDigit = 0 AND SUBSTRING(@Value, @Index, 1) NOT LIKE N'[0-9]')
          )
        BEGIN
            SET @Chunk = @Chunk + SUBSTRING(@Value, @Index, 1);
            SET @Index = @Index + 1;
        END

        IF @IsDigit = 1
        BEGIN
            IF LEN(@Chunk) < 12
                SET @Chunk = REPLICATE(N'0', 12 - LEN(@Chunk)) + @Chunk;
        END
        ELSE
        BEGIN
            SET @Chunk = LOWER(@Chunk);
        END

        SET @Result = @Result + @Chunk;
    END

    RETURN @Result;
END
