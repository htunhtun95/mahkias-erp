CREATE OR ALTER PROCEDURE [dbo].[UpdateActivity]
(
    @Id INT,
    @ProjectId INT = NULL,
    @DSNNo NVARCHAR(100),
    @PartNo NVARCHAR(MAX),
    @TypeId INT,
    @Quantity INT = 1,
    @Budget DECIMAL(18, 2) = NULL,
    @Description NVARCHAR(MAX) = NULL
)
AS
BEGIN
    SET NOCOUNT ON;

    IF @PartNo IS NULL
       OR NULLIF(LTRIM(RTRIM(REPLACE(REPLACE(@PartNo, CHAR(13), N' '), CHAR(10), N' '))), N'') IS NULL
       OR NULLIF(LTRIM(RTRIM(@DSNNo)), N'') IS NULL
       OR NULLIF(@TypeId, 0) IS NULL
    BEGIN
        SELECT Updated = 0;
        RETURN;
    END

    UPDATE projects.Activities
    SET
        DSNNo = LTRIM(RTRIM(@DSNNo)),
        PartNo = @PartNo,
        ActivityTypeId = @TypeId,
        Quantity = @Quantity,
        Budget = @Budget,
        Description = NULLIF(@Description, N''),
        ModifiedAt = GETDATE()
    WHERE Id = @Id
      AND IsDeleted = 0
      AND (@ProjectId IS NULL OR ProjectId = @ProjectId);

    DECLARE @Updated INT = @@ROWCOUNT;

    IF @Updated > 0
    BEGIN
        DECLARE @ResolvedProjectId INT = COALESCE(
            @ProjectId,
            (SELECT ProjectId FROM projects.Activities WHERE Id = @Id)
        );
        EXEC dbo.SaveActivityAlternativeParts
            @ActivityId = @Id,
            @ProjectId = @ResolvedProjectId,
            @PartNo = @PartNo;
    END

    SELECT Updated = @Updated;
END
