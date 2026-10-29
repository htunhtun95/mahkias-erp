CREATE PROCEDURE [dbo].[DeleteActivity]
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

    IF @@ROWCOUNT = 0
    BEGIN
        SELECT Deleted = 0;
        RETURN;
    END

    DECLARE @ProjectId INT;
    DECLARE @GroupId INT;
    SELECT @ProjectId = ProjectId, @GroupId = ActivityGroupId
    FROM projects.Activities
    WHERE Id = @Id;

    IF @GroupId IS NOT NULL
       AND NOT EXISTS (
            SELECT 1
            FROM projects.Activities
            WHERE ActivityGroupId = @GroupId
              AND IsDeleted = 0
       )
    BEGIN
        UPDATE projects.QuotationItems
        SET ActivityGroupId = NULL
        WHERE ActivityGroupId = @GroupId;

        UPDATE projects.Activities
        SET ActivityGroupId = NULL
        WHERE ActivityGroupId = @GroupId;

        DELETE FROM projects.ActivityGroups
        WHERE Id = @GroupId
          AND ProjectId = @ProjectId;
    END

    SELECT Deleted = 1;
END
