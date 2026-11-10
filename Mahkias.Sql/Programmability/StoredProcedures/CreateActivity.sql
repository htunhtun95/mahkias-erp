CREATE PROCEDURE [dbo].[CreateActivity]
(
    @ProjectId INT,
    @PartNo NVARCHAR(MAX),
    @Budget DECIMAL(18, 2) = NULL,
    @Description NVARCHAR(MAX) = NULL,
    @DSNNo NVARCHAR(100) = NULL,
    @Quantity INT = NULL,
    @TypeId INT = NULL,
    @Unit NVARCHAR(100) = NULL,
    @Condition NVARCHAR(100) = NULL,
    @SerialNo NVARCHAR(100) = NULL,
    @TSN NVARCHAR(100) = NULL,
    @TSO NVARCHAR(100) = NULL,
    @CSN NVARCHAR(100) = NULL,
    @CSO NVARCHAR(100) = NULL
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
        Unit,
        Condition,
        SerialNo,
        TSN,
        TSO,
        CSN,
        CSO,
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
        NULLIF(LTRIM(RTRIM(@Unit)), N''),
        NULLIF(LTRIM(RTRIM(@Condition)), N''),
        NULLIF(LTRIM(RTRIM(@SerialNo)), N''),
        NULLIF(LTRIM(RTRIM(@TSN)), N''),
        NULLIF(LTRIM(RTRIM(@TSO)), N''),
        NULLIF(LTRIM(RTRIM(@CSN)), N''),
        NULLIF(LTRIM(RTRIM(@CSO)), N''),
        @TypeId,
        GETDATE()
    );

    DECLARE @Id INT = CAST(SCOPE_IDENTITY() AS INT);
    EXEC dbo.SaveActivityAlternativeParts @ActivityId = @Id, @ProjectId = @ProjectId, @PartNo = @PartNo;

    SELECT Id = @Id;
END
