CREATE PROCEDURE [dbo].[SetProjectDocumentsFolder]
(
    @ProjectId INT,
    @DocumentsFolderId NVARCHAR(128)
)
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE projects.Projects
    SET DocumentsFolderId = NULLIF(LTRIM(RTRIM(@DocumentsFolderId)), N'')
    WHERE Id = @ProjectId
      AND IsDeleted = 0;

    SELECT DocumentsFolderId
    FROM projects.Projects
    WHERE Id = @ProjectId
      AND IsDeleted = 0;
END
