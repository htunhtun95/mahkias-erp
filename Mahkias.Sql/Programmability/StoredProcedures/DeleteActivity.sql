CREATE OR ALTER PROCEDURE [dbo].[DeleteActivity]
(
    @Id INT,
    @DeletedBy NVARCHAR(100) = NULL
)
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE projects.Activities
    SET IsDeleted = 1,
        DeletedAt = GETUTCDATE(),
        DeletedBy = @DeletedBy
    WHERE Id = @Id
      AND IsDeleted = 0;

    SELECT Deleted = @@ROWCOUNT;
END
