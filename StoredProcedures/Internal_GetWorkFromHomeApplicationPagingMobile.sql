CREATE OR ALTER PROCEDURE [dbo].[Internal_GetWorkFromHomeApplicationPagingMobile](@countSkip INT, @pageSize INT, @userId INT, @type INT, @status INT, @searchUserId INT, @searchReviewerId INT, @fromDate DATETIME, @toDate DATETIME, @isNoPaging BIT = 0)
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
            SET @WHERE_SQL = @WHERE_SQL + N' and wfh.UserId = ' + CONVERT(NVARCHAR(MAX), @userId);
        END;
        ELSE IF @type = 1
        BEGIN
            SET @WHERE_SQL = @WHERE_SQL + N' and wfh.ReviewUserId = ' + CONVERT(NVARCHAR(MAX), @userId);
        END;
        ELSE IF @type = 2
        BEGIN
            SET @WHERE_SQL = @WHERE_SQL + N' and wfh.RelatedUserId = ' + CONVERT(NVARCHAR(MAX), @userId);
        END;
    END;
    IF @status IS NOT NULL
    BEGIN
        IF @status = 0
        BEGIN
            SET @WHERE_SQL = @WHERE_SQL + N' and wfh.ReviewStatus = 0';
        END;
        ELSE IF @status = 1
        BEGIN
            SET @WHERE_SQL = @WHERE_SQL + N' and wfh.ReviewStatus in (1, 2)';
        END;
    END;
    IF @searchUserId IS NOT NULL
    BEGIN
        SET @WHERE_SQL = @WHERE_SQL + N' and wfh.UserId = ' + CONVERT(NVARCHAR(MAX), @searchUserId);
    END;
    IF @searchReviewerId IS NOT NULL
    BEGIN
        SET @WHERE_SQL = @WHERE_SQL + N' and wfh.ReviewUserId = ' + CONVERT(NVARCHAR(MAX), @searchReviewerId);
    END;
    IF @fromDate IS NOT NULL
       AND @toDate IS NOT NULL
    BEGIN
        SET @WHERE_SQL = @WHERE_SQL + N'and not ( convert(date, wfh.FromDate) >''' + CONVERT(NVARCHAR(100), @toDate) + N''' or convert(date, wfh.ToDate) <''' + CONVERT(NVARCHAR(100), @fromDate) + N''')';
    END;
    SET @SEARCH_SQL = N'
;with paging as (
	Select	ROW_NUMBER() OVER (ORDER BY wfh.FromDate DESC) as RowCounts,
		wfh.Id,
		cast(wfh.RegisterDate as Date) as RegisterDate,
		wfh.FromDate as FromDate,
		wfh.ToDate as ToDate,
		wfh.Note,
		wfh.ReviewStatus as Status,
		wfh.ReviewNote,
		Cast(wfh.ReviewDate as Date) as ReviewDate,
		wfh.PeriodType,
		
		wfh.UserId,
		ISNULL(ui.FullName,ui.Name) as UserName,
		ui.JobTitle as UserJobTitle,
		ui.Avatar as UserAvatar,
		ui.Gender as UserGender,

		wfh.ReviewUserId,
		ISNULL(reviewUser.FullName,reviewUser.Name) as ReviewUserName,
		reviewUser.JobTitle as ReviewUserJobTitle,
		reviewUser.Avatar as ReviewUserAvatar,
		reviewUser.Gender as ReviewUserGender,

		wfh.RelatedUserIds
	from [WorkFromHomeApplication] wfh
	join [UserInternal] ui on ui.id = wfh.UserId
	join [UserInternal] reviewUser on reviewUser.Id = wfh.ReviewUserId
' + @WHERE_SQL;
    SET @SEARCH_SQL = @SEARCH_SQL + N') select count(*) TotalRecord, 
		0 Id,
		null [RegisterDate],
		GETUTCDATE() [FromDate],
		GETUTCDATE() [ToDate],
		null [Note],
		0 Status,
		null [ReviewNote],
		null [ReviewDate],
		0 [PeriodType],
		
		0 [UserId],
		null as UserName,
		null UserJobTitle,
		null UserAvatar,
		null UserGender,
		0 [ReviewUserId],

		null ReviewUserName,
		null ReviewUserJobTitle,
		null ReviewUserAvatar,
		null ReviewUserGender,

		null [RelatedUserIds]
	from paging p

	union all

	select * from paging p ';
    IF @isNoPaging = 0
    BEGIN
        SET @SEARCH_SQL = @SEARCH_SQL + N' where p.RowCounts > ' + CONVERT(NVARCHAR(10), (@countSkip * @pageSize)) + N' and p.RowCounts <= ' + CONVERT(NVARCHAR(10), ((@countSkip + 1) * @pageSize)) + N'';
    END;
    SET @ParamDefinition = N'@countSkip_in INT,
							@pageSize_in INT,
							@userId_in INT,
							@type_in INT,
							@status_in INT,
							@searchUserId_in INT,
							@fromDate_in Datetime,
							@toDate_in Datetime,
							@isNoPaging_in bit';
    PRINT @SEARCH_SQL;
    EXECUTE sp_executesql
        @SEARCH_SQL, @ParamDefinition, @countSkip_in = @countSkip, @pageSize_in = @pageSize, @userId_in = @userId, @type_in = @type, @status_in = @status, @searchUserId_in = @searchUserId, @fromDate_in = @fromDate, @toDate_in = @toDate, @isNoPaging_in = @isNoPaging;
END;