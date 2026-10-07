CREATE OR ALTER PROCEDURE [dbo].[SearchProjects]
(
    @SearchTerm NVARCHAR(200) = NULL,
    @PageNumber INT = 1,
    @PageSize INT = 50,
    @SortBy NVARCHAR(50) = N'createdAt',
    @SortDirection NVARCHAR(4) = N'desc'
)
AS
BEGIN
    SET NOCOUNT ON;

    IF (@PageNumber IS NULL OR @PageNumber < 1)
        SET @PageNumber = 1;

    IF (@PageSize IS NULL OR @PageSize < 1)
        SET @PageSize = 50;

    DECLARE @Offset INT = (@PageNumber - 1) * @PageSize;
    DECLARE @Term NVARCHAR(200) = NULLIF(LTRIM(RTRIM(@SearchTerm)), N'');
    DECLARE @SortColumn NVARCHAR(50) = LOWER(ISNULL(NULLIF(LTRIM(RTRIM(@SortBy)), N''), N'createdAt'));
    DECLARE @SortDesc BIT = CASE
        WHEN LOWER(LTRIM(RTRIM(ISNULL(@SortDirection, N'desc')))) = N'asc' THEN 0
        ELSE 1
    END;

    IF @SortColumn IN (N'code', N'reference')
        SET @SortColumn = N'reference';

    IF @SortColumn NOT IN (N'name', N'reference', N'createdat', N'modifiedat')
        SET @SortColumn = N'createdat';

    SELECT
        P.Id,
        P.Name,
        P.Reference,
        P.Description,
        P.CreatedAt,
        P.ModifiedAt
    FROM projects.Projects AS P
    WHERE P.IsDeleted = 0
      AND (
        @Term IS NULL
        OR CHARINDEX(LOWER(@Term), LOWER(P.Name)) > 0
        OR CHARINDEX(LOWER(@Term), LOWER(ISNULL(P.Reference, N''))) > 0
        OR CHARINDEX(LOWER(@Term), LOWER(ISNULL(P.Description, N''))) > 0
      )
    ORDER BY
        CASE WHEN @SortDesc = 0 AND @SortColumn = N'name' THEN P.Name END ASC,
        CASE WHEN @SortDesc = 1 AND @SortColumn = N'name' THEN P.Name END DESC,
        CASE WHEN @SortDesc = 0 AND @SortColumn = N'reference' THEN P.Reference END ASC,
        CASE WHEN @SortDesc = 1 AND @SortColumn = N'reference' THEN P.Reference END DESC,
        CASE WHEN @SortDesc = 0 AND @SortColumn = N'createdat' THEN P.CreatedAt END ASC,
        CASE WHEN @SortDesc = 1 AND @SortColumn = N'createdat' THEN P.CreatedAt END DESC,
        CASE WHEN @SortDesc = 0 AND @SortColumn = N'modifiedat' THEN P.ModifiedAt END ASC,
        CASE WHEN @SortDesc = 1 AND @SortColumn = N'modifiedat' THEN P.ModifiedAt END DESC,
        P.Id DESC
    OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;

    SELECT Total_Results = COUNT(*)
    FROM projects.Projects AS P
    WHERE P.IsDeleted = 0
      AND (
        @Term IS NULL
        OR CHARINDEX(LOWER(@Term), LOWER(P.Name)) > 0
        OR CHARINDEX(LOWER(@Term), LOWER(ISNULL(P.Reference, N''))) > 0
        OR CHARINDEX(LOWER(@Term), LOWER(ISNULL(P.Description, N''))) > 0
      );
END
