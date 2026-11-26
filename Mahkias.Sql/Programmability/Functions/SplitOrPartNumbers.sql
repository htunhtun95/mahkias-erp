-- Uses the Activities alternate-part rule: or, (or), and a slash between two full part numbers.
CREATE FUNCTION [dbo].[SplitOrPartNumbers]
(
    @PartNo NVARCHAR(MAX)
)
RETURNS @Result TABLE
(
    SortOrder INT NOT NULL,
    PartValue NVARCHAR(MAX) NOT NULL
)
AS
BEGIN
    INSERT INTO @Result (SortOrder, PartValue)
    SELECT SortOrder, PartValue
    FROM dbo.SplitAlternativePartNumbers(@PartNo);

    RETURN;
END
