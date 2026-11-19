CREATE FUNCTION [dbo].[SplitAlternativePartNumbers]
(
    @PartNo NVARCHAR(MAX)
)
RETURNS @Result TABLE
(
    SortOrder INT NOT NULL,
    PartValue NVARCHAR(MAX) NOT NULL,
    IsMain BIT NOT NULL
)
AS
BEGIN
    IF @PartNo IS NULL
        RETURN;

    DECLARE @Collapsed NVARCHAR(MAX) = REPLACE(REPLACE(@PartNo, CHAR(13), N' '), CHAR(10), N' ');
    IF NULLIF(LTRIM(RTRIM(@Collapsed)), N'') IS NULL
        RETURN;

    DECLARE @Work NVARCHAR(MAX) = @PartNo;
    DECLARE @Lower NVARCHAR(MAX) = LOWER(@Work);
    DECLARE @Pattern NVARCHAR(20);
    DECLARE @Pos INT;
    DECLARE @Patterns TABLE (Pattern NVARCHAR(20), PatternOrder INT);

    INSERT INTO @Patterns (Pattern, PatternOrder)
    VALUES
        (N'(  or  )', 1),
        (N'( or )', 2),
        (N'(or)', 3),
        (N' or ', 4);

    DECLARE pattern_cursor CURSOR LOCAL FAST_FORWARD FOR
        SELECT Pattern FROM @Patterns ORDER BY PatternOrder;

    OPEN pattern_cursor;
    FETCH NEXT FROM pattern_cursor INTO @Pattern;

    WHILE @@FETCH_STATUS = 0
    BEGIN
        SET @Lower = LOWER(@Work);
        WHILE CHARINDEX(@Pattern, @Lower) > 0
        BEGIN
            SET @Pos = CHARINDEX(@Pattern, @Lower);
            SET @Work = STUFF(@Work, @Pos, LEN(@Pattern), N'|');
            SET @Lower = STUFF(@Lower, @Pos, LEN(@Pattern), N'|');
        END

        FETCH NEXT FROM pattern_cursor INTO @Pattern;
    END

    CLOSE pattern_cursor;
    DEALLOCATE pattern_cursor;

    DECLARE @SlashPos INT = 1;
    DECLARE @TokenEnd INT;
    DECLARE @NextSlash INT;
    DECLARE @NextBar INT;
    DECLARE @Token NVARCHAR(MAX);
    DECLARE @LeftBound INT;
    DECLARE @Scan INT;
    DECLARE @Found INT;
    DECLARE @LeftToken NVARCHAR(MAX);

    -- A slash separates alternate parts only when both sides are full part numbers.
    -- Fractions such as 1/2 and short revision suffixes such as / B stay in one part.
    WHILE 1 = 1
    BEGIN
        SET @SlashPos = CHARINDEX(N'/', @Work, @SlashPos);
        IF @SlashPos = 0
            BREAK;

        SET @TokenEnd = LEN(@Work) + 1;
        SET @NextSlash = CHARINDEX(N'/', @Work, @SlashPos + 1);
        SET @NextBar = CHARINDEX(N'|', @Work, @SlashPos + 1);
        IF @NextSlash > 0 AND @NextSlash < @TokenEnd
            SET @TokenEnd = @NextSlash;
        IF @NextBar > 0 AND @NextBar < @TokenEnd
            SET @TokenEnd = @NextBar;

        SET @Token = LTRIM(RTRIM(SUBSTRING(@Work, @SlashPos + 1, @TokenEnd - @SlashPos - 1)));

        SET @LeftBound = 0;
        SET @Scan = 1;
        WHILE 1 = 1
        BEGIN
            SET @Found = CHARINDEX(N'|', @Work, @Scan);
            IF @Found = 0 OR @Found >= @SlashPos
                BREAK;
            SET @LeftBound = @Found;
            SET @Scan = @Found + 1;
        END

        SET @LeftToken = LTRIM(RTRIM(SUBSTRING(@Work, @LeftBound + 1, @SlashPos - @LeftBound - 1)));

        IF LEN(@LeftToken) > 3
           AND CHARINDEX(N' ', @LeftToken) = 0
           AND CHARINDEX(CHAR(9), @LeftToken) = 0
           AND CHARINDEX(N'/', @LeftToken) = 0
           AND @LeftToken LIKE N'%[^0-9]%'
           AND LEN(@Token) > 3
           AND CHARINDEX(N' ', @Token) = 0
           AND CHARINDEX(CHAR(9), @Token) = 0
           AND CHARINDEX(N'/', @Token) = 0
           AND @Token LIKE N'%[^0-9]%'
        BEGIN
            SET @Work = STUFF(@Work, @SlashPos, 1, N'|');
        END

        SET @SlashPos = @SlashPos + 1;
    END

    DECLARE @SortOrder INT = 0;
    DECLARE @Start INT = 1;
    DECLARE @Delim INT;
    DECLARE @Piece NVARCHAR(MAX);

    WHILE @Start <= LEN(@Work) + 1
    BEGIN
        SET @Delim = CHARINDEX(N'|', @Work, @Start);
        IF @Delim = 0
            SET @Piece = SUBSTRING(@Work, @Start, LEN(@Work) - @Start + 1);
        ELSE
            SET @Piece = SUBSTRING(@Work, @Start, @Delim - @Start);

        SET @Piece = LTRIM(RTRIM(@Piece));
        WHILE LEN(@Piece) > 0 AND UNICODE(LEFT(@Piece, 1)) IN (9, 10, 13)
            SET @Piece = SUBSTRING(@Piece, 2, LEN(@Piece));
        WHILE LEN(@Piece) > 0 AND UNICODE(RIGHT(@Piece, 1)) IN (9, 10, 13)
            SET @Piece = LEFT(@Piece, LEN(@Piece) - 1);

        IF NULLIF(@Piece, N'') IS NOT NULL
        BEGIN
            SET @SortOrder = @SortOrder + 1;
            INSERT INTO @Result (SortOrder, PartValue, IsMain)
            VALUES (@SortOrder, @Piece, CASE WHEN @SortOrder = 1 THEN 1 ELSE 0 END);
        END

        IF @Delim = 0
            BREAK;

        SET @Start = @Delim + 1;
    END

    RETURN;
END
