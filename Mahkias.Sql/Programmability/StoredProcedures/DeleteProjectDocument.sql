CREATE PROCEDURE [dbo].[DeleteProjectDocument]
(
    @ProjectId INT,
    @DocumentId INT
)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @AssetId UNIQUEIDENTIFIER;
    DECLARE @Deleted INT = 0;

    SELECT @AssetId = D.AssetId
    FROM projects.ProjectDocuments AS D
    INNER JOIN projects.Projects AS P ON P.Id = D.ProjectId
    WHERE D.DocumentId = @DocumentId
      AND D.ProjectId = @ProjectId
      AND P.IsDeleted = 0;

    DELETE D
    FROM projects.ProjectDocuments AS D
    WHERE D.DocumentId = @DocumentId
      AND D.ProjectId = @ProjectId
      AND D.AssetId = @AssetId;

    SET @Deleted = @@ROWCOUNT;

    IF @Deleted > 0 AND @AssetId IS NOT NULL
       AND NOT EXISTS (SELECT 1 FROM projects.ProjectDocuments WHERE AssetId = @AssetId)
       AND NOT EXISTS (SELECT 1 FROM projects.QuotationDocuments WHERE AssetId = @AssetId)
       AND NOT EXISTS (SELECT 1 FROM projects.PurchaseOrderDocuments WHERE AssetId = @AssetId)
    BEGIN
        DELETE FROM media.assets WHERE id = @AssetId;
    END

    SELECT Deleted = @Deleted;
END
