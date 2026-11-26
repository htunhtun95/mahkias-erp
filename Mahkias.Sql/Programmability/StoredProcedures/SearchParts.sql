CREATE PROCEDURE [dbo].[SearchParts]
(
    @Query NVARCHAR(200) = NULL
)
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Text NVARCHAR(200) = LOWER(LTRIM(RTRIM(@Query)));
    IF @Text IS NULL OR @Text = N''
    BEGIN
        SELECT TOP (0)
            PartId = CAST(NULL AS INT),
            PartNo = CAST(NULL AS NVARCHAR(MAX)),
            Description = CAST(NULL AS NVARCHAR(MAX));
        RETURN;
    END

    DECLARE @Like NVARCHAR(220) =
        N'%' +
        REPLACE(REPLACE(REPLACE(REPLACE(@Text, N'\', N'\\'), N'%', N'\%'), N'_', N'\_'), N'[', N'\[') +
        N'%';

    SELECT TOP (30)
        PartId = P.Id,
        P.PartNo,
        D.Description
    FROM projects.Parts AS P
    LEFT JOIN projects.PartDescriptions AS D
        ON D.PartId = P.Id
    WHERE LOWER(P.PartNo) LIKE @Like ESCAPE N'\'
       OR LOWER(D.Description) LIKE @Like ESCAPE N'\'
    ORDER BY P.PartNo, P.Id, D.Id;
END
