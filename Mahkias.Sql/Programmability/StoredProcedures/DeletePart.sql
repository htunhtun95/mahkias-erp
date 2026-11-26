CREATE PROCEDURE [dbo].[DeletePart]
(
    @Id INT
)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF NOT EXISTS (SELECT 1 FROM projects.Parts WHERE Id = @Id)
    BEGIN
        SELECT Deleted = 0;
        RETURN;
    END

    BEGIN TRY
        BEGIN TRANSACTION;

        UPDATE projects.Activities
        SET PartId = NULL
        WHERE PartId = @Id;

        DELETE FROM projects.PartAlternates
        WHERE PartId = @Id
           OR AlternatePartId = @Id;

        DELETE FROM projects.PartDescriptions
        WHERE PartId = @Id;

        DELETE FROM projects.Parts
        WHERE Id = @Id;

        COMMIT TRANSACTION;
        SELECT Deleted = 1;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
        BEGIN
            ROLLBACK TRANSACTION;
        END
        ;THROW
    END CATCH
END
