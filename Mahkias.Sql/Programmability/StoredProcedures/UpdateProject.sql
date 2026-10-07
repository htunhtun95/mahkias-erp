CREATE OR ALTER PROCEDURE [dbo].[UpdateProject]
(
    @Id INT,
    @Name NVARCHAR(250),
    @Reference NVARCHAR(100),
    @Description NVARCHAR(MAX)
)
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT EXISTS (SELECT 1 FROM projects.Projects WHERE Id = @Id AND IsDeleted = 0)
    BEGIN
        SELECT StatusCode = 0, Message = N'Project not found.';
        RETURN;
    END

    DECLARE @Ref NVARCHAR(100) = NULLIF(LTRIM(RTRIM(@Reference)), N'');

    IF @Ref IS NOT NULL
       AND EXISTS (
            SELECT 1
            FROM projects.Projects
            WHERE IsDeleted = 0
              AND LOWER(LTRIM(RTRIM(Reference))) = LOWER(@Ref)
              AND Id <> @Id
       )
    BEGIN
        SELECT StatusCode = -1, Message = N'Reference code already exists.';
        RETURN;
    END

    UPDATE projects.Projects
    SET
        Name = LTRIM(RTRIM(@Name)),
        Reference = @Ref,
        Description = @Description,
        ModifiedAt = GETDATE()
    WHERE Id = @Id
      AND IsDeleted = 0;

    SELECT StatusCode = 1, Message = CAST(NULL AS NVARCHAR(200));
END
