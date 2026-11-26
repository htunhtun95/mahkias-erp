CREATE PROCEDURE [dbo].[ListParts]
(
    @Search NVARCHAR(200) = NULL
)
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Text NVARCHAR(200) = LOWER(LTRIM(RTRIM(@Search)));
    DECLARE @Like NVARCHAR(220) = NULL;
    IF @Text IS NOT NULL AND @Text <> N''
    BEGIN
        SET @Like =
            N'%' +
            REPLACE(REPLACE(REPLACE(REPLACE(@Text, N'\', N'\\'), N'%', N'\%'), N'_', N'\_'), N'[', N'\[') +
            N'%';
    END

    SELECT
        P.Id,
        P.PartNo,
        P.CreatedAt,
        Descriptions =
        (
            SELECT D.Id AS id, D.Description AS description
            FROM projects.PartDescriptions AS D
            WHERE D.PartId = P.Id
            ORDER BY D.Id
            FOR JSON PATH
        )
    FROM projects.Parts AS P
    WHERE @Like IS NULL
       OR LOWER(P.PartNo) LIKE @Like ESCAPE N'\'
    ORDER BY P.PartNo, P.Id;
END
