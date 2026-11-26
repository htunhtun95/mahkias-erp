CREATE PROCEDURE [dbo].[GetPart]
(
    @Id INT
)
AS
BEGIN
    SET NOCOUNT ON;

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
        ),
        Message = CAST(NULL AS NVARCHAR(400))
    FROM projects.Parts AS P
    WHERE P.Id = @Id;
END
