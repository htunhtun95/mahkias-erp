CREATE PROCEDURE [dbo].[CreateActivity]
(
    @ProjectId INT,
    @PartNo NVARCHAR(MAX),
    @Budget DECIMAL(18, 2) = NULL,
    @Description NVARCHAR(MAX) = NULL,
    @DSNNo NVARCHAR(100) = NULL,
    @Quantity INT = NULL,
    @TypeId INT = NULL
)
AS
BEGIN
    SET NOCOUNT ON;

    IF @PartNo IS NULL
       OR NULLIF(LTRIM(RTRIM(REPLACE(REPLACE(@PartNo, CHAR(13), N' '), CHAR(10), N' '))), N'') IS NULL
    BEGIN
        SELECT Id = CAST(NULL AS INT);
        RETURN;
    END

    INSERT INTO projects.Activities
    (
        ProjectId,
        PartNo,
        Budget,
        Description,
        DSNNo,
        Quantity,
        ActivityTypeId,
        CreatedAt
    )
    VALUES
    (
        @ProjectId,
        @PartNo,
        @Budget,
        @Description,
        @DSNNo,
        @Quantity,
        @TypeId,
        GETDATE()
    );

    DECLARE @Id INT = CAST(SCOPE_IDENTITY() AS INT);
    EXEC dbo.SaveActivityAlternativeParts @ActivityId = @Id, @ProjectId = @ProjectId, @PartNo = @PartNo;

    SELECT Id = @Id;
END
