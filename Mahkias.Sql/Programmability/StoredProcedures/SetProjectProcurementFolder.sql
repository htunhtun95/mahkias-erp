CREATE OR ALTER PROCEDURE [dbo].[SetProjectProcurementFolder]
(
    @ProjectId INT,
    @ProcurementFolderId NVARCHAR(128)
)
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE projects.Projects
    SET ProcurementFolderId = NULLIF(LTRIM(RTRIM(@ProcurementFolderId)), N'')
    WHERE Id = @ProjectId
      AND IsDeleted = 0;

    SELECT ProcurementFolderId
    FROM projects.Projects
    WHERE Id = @ProjectId
      AND IsDeleted = 0;
END
