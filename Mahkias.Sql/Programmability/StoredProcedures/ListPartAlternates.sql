CREATE PROCEDURE [dbo].[ListPartAlternates]
(
    @PartId INT
)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        L.Id,
        PartId = P.Id,
        P.PartNo,
        Descriptions =
        (
            SELECT D.Id AS id, D.Description AS description
            FROM projects.PartDescriptions AS D
            WHERE D.PartId = P.Id
            ORDER BY D.Id
            FOR JSON PATH
        ),
        Message = CAST(NULL AS NVARCHAR(400))
    FROM projects.PartAlternates AS L
    INNER JOIN projects.Parts AS P
        ON P.Id = L.AlternatePartId
    WHERE L.PartId = @PartId
    ORDER BY P.PartNo, L.Id;
END
