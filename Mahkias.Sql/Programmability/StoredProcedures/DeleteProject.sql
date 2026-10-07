CREATE OR ALTER PROCEDURE [dbo].[DeleteProject]
(
    @Id INT,
    @DeletedBy NVARCHAR(100) = NULL,
    @Success BIT OUTPUT
)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    SET @Success = 0;

    IF NOT EXISTS (SELECT 1 FROM projects.Projects WHERE Id = @Id AND IsDeleted = 0)
    BEGIN
        RETURN;
    END

    BEGIN TRY
        BEGIN TRANSACTION;

        DECLARE @Now DATETIME2 = GETUTCDATE();

        UPDATE projects.Activities
        SET IsDeleted = 1,
            DeletedAt = @Now,
            DeletedBy = @DeletedBy
        WHERE ProjectId = @Id
          AND IsDeleted = 0;

        UPDATE projects.Projects
        SET IsDeleted = 1,
            DeletedAt = @Now,
            DeletedBy = @DeletedBy
        WHERE Id = @Id
          AND IsDeleted = 0;

        COMMIT TRANSACTION;
        SET @Success = 1;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
        BEGIN
            ROLLBACK TRANSACTION;
        END

        SET @Success = 0;
        THROW;
    END CATCH
END
