CREATE PROCEDURE [dbo].[GetProjectDriveFolder]
(
    @ProjectId INT
)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        Name,
        Reference,
        GoogleDriveFolderId,
        ProcurementFolderId,
        DocumentsFolderId
    FROM projects.Projects
    WHERE Id = @ProjectId
      AND IsDeleted = 0;
END
