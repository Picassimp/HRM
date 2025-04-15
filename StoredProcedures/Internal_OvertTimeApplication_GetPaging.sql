CREATE OR ALTER PROCEDURE [dbo].[Internal_OvertTimeApplication_GetPaging]
(
	@countSkip INT, 
	@pageSize INT, 
	@userId INT, 
	@type INT, 
	@status INT, 
	@searchUserId INT, 
	@searchReviewerId INT, 
	@fromDate DATETIME, 
	@toDate DATETIME, 
	@isNoPaging BIT = 0,
	@keysort VARCHAR(50),
	@orderByDescending BIT
	)
AS
BEGIN
    DECLARE @WHERE_SQL NVARCHAR(MAX);
    DECLARE @ParamDefinition NVARCHAR(MAX);
    DECLARE @SEARCH_SQL NVARCHAR(MAX);
    SET @WHERE_SQL = N' WHERE 1 = 1 ';
    IF @type IS NOT NULL
    BEGIN
        IF @type = 0
        BEGIN
            SET @WHERE_SQL = @WHERE_SQL + N' AND o.UserId = ' + CONVERT(NVARCHAR(MAX), @userId);
        END;
        ELSE IF @type = 1
        BEGIN
            SET @WHERE_SQL = @WHERE_SQL + N' AND o.ReviewUserId = ' + CONVERT(NVARCHAR(MAX), @userId);
        END;
    END;
    IF @status IS NOT NULL
    BEGIN
        SET @WHERE_SQL = @WHERE_SQL + N' AND o.Status = ' + CONVERT(NVARCHAR(MAX), @status);
    END;
    IF @searchUserId IS NOT NULL
    BEGIN
        SET @WHERE_SQL = @WHERE_SQL + N' AND o.userId = ' + CONVERT(NVARCHAR(MAX), @searchUserId);
    END;
    IF @searchReviewerId IS NOT NULL
    BEGIN
        SET @WHERE_SQL = @WHERE_SQL + N' AND o.ReviewUserId = ' + CONVERT(NVARCHAR(MAX), @searchReviewerId);
    END;
    IF @fromDate IS NOT NULL
       AND @toDate IS NOT NULL
    BEGIN
        SET @WHERE_SQL = @WHERE_SQL + N'AND NOT ( CONVERT(date, o.FromDate) >''' + CONVERT(NVARCHAR(100), @toDate) + N''' OR CONVERT(date, o.ToDate) <''' + CONVERT(NVARCHAR(100), @fromDate) + N''')';
    END;
    SET @SEARCH_SQL = N';WITH paging AS (
	SELECT
        ROW_NUMBER() OVER (ORDER BY 
			IIF(COALESCE(@keysort_in, '''') = '''', o.RegisterDate, NULL) DESC,
			IIF(@keysort_in = ''RegisterDate'' and @orderByDescending_in = 0, o.RegisterDate, NULL) ASC,
			IIF(@keysort_in = ''RegisterDate'' and @orderByDescending_in = 1, o.RegisterDate, NULL) DESC,
			IIF(@keysort_in = ''FromDate'' and @orderByDescending_in = 0, o.FromDate, NULL) ASC,
			IIF(@keysort_in = ''FromDate'' and @orderByDescending_in = 1, o.FromDate, NULL) DESC,
			IIF(@keysort_in = ''RegisterName'' and @orderByDescending_in = 0, userRegist.FullName, NULL) ASC,
			IIF(@keysort_in = ''RegisterName'' and @orderByDescending_in = 1, userRegist.FullName, NULL) DESC
		) AS RowCounts, 
        o.Id, 
        o.RegisterDate, 
        o.FromDate AS FromDate, 
        o.ToDate AS ToDate, 
        o.OverTimeHour, 
        o.OverTimeNote, 
        o.ReviewNote, 
        o.ReviewDate, 
        o.Status, 
        o.CreatedDate, 
        o.UpdatedDate, 
        o.BreakHour, 
        o.UserId, 
        o.ReviewUserId, 
        ISNULL(userRegist.FullName, userRegist.Name) AS UserName, 
        ISNULL(UserInternal.FullName, UserInternal.Name) AS ReviewUser, userRegist.JobTitle
    FROM dbo.OverTimeApplication o
    JOIN dbo.UserInternal ON UserInternal.Id = o.ReviewUserId
    JOIN dbo.UserInternal userRegist ON userRegist.id = o.UserId ' + @WHERE_SQL;
    SET @SEARCH_SQL = @SEARCH_SQL + N') SELECT COUNT(*) TotalRecord, 
		0 Id,
		null [RegisterDate],
		null [FromDate],
		null [ToDate],
		null [OverTimeHour],
		null [OverTimeNote],
		null [ReviewNote],
		null [ReviewDate],
		null [Status],
		GETDATE() [CreatedDate],
		null [UpdatedDate],
		null [BreakHour],
		null [UserId],
		null [ReviewUserId],
		null [UserName],
		null [ReviewUser],
		null [JobTitle]
	FROM paging p
	UNION ALL
	SELECT * FROM paging p';
    IF @isNoPaging = 0
    BEGIN
        SET @SEARCH_SQL = @SEARCH_SQL + N' WHERE p.RowCounts > ' + CONVERT(NVARCHAR(10), (@countSkip * @pageSize)) + N' AND p.RowCounts <= ' + CONVERT(NVARCHAR(10), ((@countSkip + 1) * @pageSize)) + N'';
    END;
    SET @ParamDefinition = N'@countSkip_in INT,
							@pageSize_in INT,
							@userId_in INT,
							@type_in INT,
							@status_in INT,
							@searchUserId_in INT,
							@fromDate_in Datetime,
							@toDate_in Datetime,
							@isNoPaging_in bit,
							@keysort_in varchar(50),
							@orderByDescending_in bit';
    PRINT @SEARCH_SQL;
    EXECUTE sp_executesql
        @SEARCH_SQL, 
		@ParamDefinition, 
		@countSkip_in = @countSkip, 
		@pageSize_in = @pageSize, 
		@userId_in = @userId, 
		@type_in = @type, 
		@status_in = @status, 
		@searchUserId_in = @searchUserId, 
		@fromDate_in = @fromDate, 
		@toDate_in = @toDate, 
		@isNoPaging_in = @isNoPaging,
		@keysort_in = @keysort,
		@orderByDescending_in = @orderByDescending;
END;