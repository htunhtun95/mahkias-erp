CREATE OR ALTER PROCEDURE [dbo].[CreateProject]
(
    @Name NVARCHAR(250),
    @Reference NVARCHAR(100),
    @Description NVARCHAR(MAX)
)
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Ref NVARCHAR(100) = NULLIF(LTRIM(RTRIM(@Reference)), N'');

    IF @Ref IS NOT NULL
       AND EXISTS (
            SELECT 1
            FROM projects.Projects
            WHERE IsDeleted = 0
              AND LOWER(LTRIM(RTRIM(Reference))) = LOWER(@Ref)
       )
    BEGIN
        SELECT Id = -1, Message = N'Reference code already exists.';
        RETURN;
    END

    INSERT INTO projects.Projects
    (
        Name,
        Reference,
        Description,
        CreatedAt
    )
    VALUES
    (
        LTRIM(RTRIM(@Name)),
        @Ref,
        @Description,
        GETDATE()
    );

    SELECT Id = CAST(SCOPE_IDENTITY() AS INT), Message = CAST(NULL AS NVARCHAR(200));
END
