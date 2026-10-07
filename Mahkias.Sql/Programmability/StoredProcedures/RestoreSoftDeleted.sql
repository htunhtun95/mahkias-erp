CREATE OR ALTER PROCEDURE [dbo].[RestoreSoftDeleted]
(
    @Entity NVARCHAR(50),
    @Id INT,
    @Success BIT OUTPUT
)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    SET @Success = 0;

    DECLARE @Name NVARCHAR(50) = LOWER(LTRIM(RTRIM(ISNULL(@Entity, N''))));

    BEGIN TRY
        BEGIN TRANSACTION;

        IF @Name = N'project'
        BEGIN
            DECLARE @DeletedAt DATETIME2;
            SELECT @DeletedAt = DeletedAt
            FROM projects.Projects
            WHERE Id = @Id
              AND IsDeleted = 1;

            IF @DeletedAt IS NOT NULL
            BEGIN
                UPDATE projects.Activities
                SET IsDeleted = 0,
                    DeletedAt = NULL,
                    DeletedBy = NULL
                WHERE ProjectId = @Id
                  AND IsDeleted = 1
                  AND DeletedAt = @DeletedAt;

                UPDATE projects.Projects
                SET IsDeleted = 0,
                    DeletedAt = NULL,
                    DeletedBy = NULL
                WHERE Id = @Id
                  AND IsDeleted = 1;

                SET @Success = 1;
            END
        END
        ELSE IF @Name = N'activity'
        BEGIN
            UPDATE projects.Activities
            SET IsDeleted = 0,
                DeletedAt = NULL,
                DeletedBy = NULL
            WHERE Id = @Id
              AND IsDeleted = 1;

            SET @Success = CASE WHEN @@ROWCOUNT > 0 THEN 1 ELSE 0 END;
        END
        ELSE IF @Name = N'quotation'
        BEGIN
            UPDATE projects.Quotations
            SET IsDeleted = 0,
                DeletedAt = NULL,
                DeletedBy = NULL
            WHERE Id = @Id
              AND IsDeleted = 1;

            SET @Success = CASE WHEN @@ROWCOUNT > 0 THEN 1 ELSE 0 END;
        END
        ELSE IF @Name = N'supplier'
        BEGIN
            UPDATE projects.Suppliers
            SET IsDeleted = 0,
                DeletedAt = NULL,
                DeletedBy = NULL
            WHERE Id = @Id
              AND IsDeleted = 1;

            SET @Success = CASE WHEN @@ROWCOUNT > 0 THEN 1 ELSE 0 END;
        END

        COMMIT TRANSACTION;
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
