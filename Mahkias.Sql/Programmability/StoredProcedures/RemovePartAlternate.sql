CREATE PROCEDURE [dbo].[RemovePartAlternate]
(
    @PartId INT,
    @LinkId INT
)
AS
BEGIN
    SET NOCOUNT ON;

    DELETE FROM projects.PartAlternates
    WHERE Id = @LinkId
      AND PartId = @PartId;

    SELECT Deleted = @@ROWCOUNT;
END
