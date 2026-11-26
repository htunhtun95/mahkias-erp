CREATE PROCEDURE [dbo].[ResolveQuotationPart]
(
    @PartId INT = NULL,
    @PartNo NVARCHAR(MAX),
    @Description NVARCHAR(MAX) = NULL
)
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Number NVARCHAR(MAX) = NULLIF(LTRIM(RTRIM(@PartNo)), N'');
    IF @Number IS NULL
    BEGIN
        RETURN;
    END

    IF (SELECT COUNT(*) FROM dbo.SplitAlternativePartNumbers(@Number)) > 1
    BEGIN
        THROW 51000, N'Enter one part number. Alternate parts are not used on quotations.', 1;
    END

    DECLARE @Text NVARCHAR(MAX) = NULLIF(LTRIM(RTRIM(@Description)), N'');
    DECLARE @TargetId INT = NULL;

    IF @PartId IS NOT NULL
       AND EXISTS
       (
           SELECT 1
           FROM projects.Parts
           WHERE Id = @PartId
             AND LOWER(LTRIM(RTRIM(PartNo))) = LOWER(@Number)
       )
    BEGIN
        SET @TargetId = @PartId;
    END
    ELSE
    BEGIN
        INSERT INTO projects.Parts (PartNo)
        VALUES (@Number);

        SET @TargetId = CAST(SCOPE_IDENTITY() AS INT);
    END

    IF @Text IS NOT NULL
       AND NOT EXISTS
       (
           SELECT 1
           FROM projects.PartDescriptions
           WHERE PartId = @TargetId
             AND LOWER(LTRIM(RTRIM(Description))) = LOWER(@Text)
       )
    BEGIN
        INSERT INTO projects.PartDescriptions (PartId, Description)
        VALUES (@TargetId, @Text);
    END
END
