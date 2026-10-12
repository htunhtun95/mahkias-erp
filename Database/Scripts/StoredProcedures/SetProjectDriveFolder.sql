CREATE OR ALTER PROCEDURE [dbo].[SetProjectDriveFolder]
(
    @ProjectId INT,
    @GoogleDriveFolderId NVARCHAR(128)
)
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE projects.Projects
    SET GoogleDriveFolderId = NULLIF(LTRIM(RTRIM(@GoogleDriveFolderId)), N'')
    WHERE Id = @ProjectId
      AND IsDeleted = 0;

    SELECT GoogleDriveFolderId
    FROM projects.Projects
    WHERE Id = @ProjectId
      AND IsDeleted = 0;
END
GO
