CREATE OR ALTER PROCEDURE [dbo].[Internal_GetLeaveApplicationPagingMobile](
@countSkip INT, 
@pageSize INT, 
@userId INT, 
@type INT, 
@status INT, 
@searchUserId INT, 
@searchReviewerId INT, 
@fromDate DATETIME, 
@toDate DATETIME,
@isNoPaging BIT = 0)
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
            SET @WHERE_SQL = @WHERE_SQL + N' and o.UserId = ' + CONVERT(NVARCHAR(MAX), @userId);
        END;
        ELSE IF @type = 1
        BEGIN
            SET @WHERE_SQL = @WHERE_SQL + N' and o.ReviewUserId = ' + CONVERT(NVARCHAR(MAX), @userId);
        END;
        ELSE IF @type = 2
        BEGIN
            SET @WHERE_SQL = @WHERE_SQL + N' and o.RelatedUserId = ' + CONVERT(NVARCHAR(MAX), @userId);
        END;
    END;
    IF @status IS NOT NULL
    BEGIN
        IF @status = 0
        BEGIN
            SET @WHERE_SQL = @WHERE_SQL + N' and o.ReviewStatus = 0';
        END;
        ELSE IF @status = 1
        BEGIN
            SET @WHERE_SQL = @WHERE_SQL + N' and o.ReviewStatus in (1, 2)';
        END;
    END;
    IF @searchUserId IS NOT NULL
    BEGIN
        SET @WHERE_SQL = @WHERE_SQL + N' and o.userId = ' + CONVERT(NVARCHAR(MAX), @searchUserId);
    END;
    IF @searchReviewerId IS NOT NULL
    BEGIN
        SET @WHERE_SQL = @WHERE_SQL + N' and o.ReviewUserId = ' + CONVERT(NVARCHAR(MAX), @searchReviewerId);
    END;
    IF @fromDate IS NOT NULL
       AND @toDate IS NOT NULL
    BEGIN
        SET @WHERE_SQL = @WHERE_SQL + N'and not ( convert(date, o.FromDate) >''' + CONVERT(NVARCHAR(100), @toDate) + N''' or convert(date, o.ToDate) <''' + CONVERT(NVARCHAR(100), @fromDate) + N''')';
    END;
    SET @SEARCH_SQL = N'
;with paging as (
	Select	ROW_NUMBER() OVER (ORDER BY o.FromDate DESC) as RowCounts,
		o.Id,
		cast(o.RegisterDate as Date) as RegisterDate,
		o.FromDate as FromDate,
		o.ToDate as ToDate,
		o.NumberDayOff,
		o.LeaveApplicationNote as Note,
		o.ReviewStatus as Status,
		o.ReviewNote,
		LeaveApplicationType.Id as TypeId,
		LeaveApplicationType.Name as TypeName,
		o.UserId,
		o.ReviewUserId,
		Cast( o.ReviewDate as Date) as ReviewDate,
		o.PeriodType,
		o.RelatedUserId,
		o.IsAlertCustomer,
		o.HandoverUserId,
		ISNULL(userRegist.FullName,userRegist.Name) as UserName,
		userRegist.JobTitle as UserJobTitle,
		userRegist.Avatar as UserAvatar,
		userRegist.Gender as UserGender,
		ISNULL(UserInternal.FullName,UserInternal.Name) as ReviewUserName,
		UserInternal.JobTitle as ReviewUserJobTitle,
		UserInternal.Avatar as ReviewUserAvatar,
		UserInternal.Gender as ReviewUserGender,
		o.RelatedUserIds
	from LeaveApplication o
	join LeaveApplicationType on o.LeaveApplicationTypeId = LeaveApplicationType.Id
	join UserInternal on UserInternal.Id = o.ReviewUserId 
	join UserInternal userRegist on userRegist.id = o.UserId
' + @WHERE_SQL;
    SET @SEARCH_SQL = @SEARCH_SQL + N') select count(*) TotalRecord, 
		0 Id,
		null [RegisterDate],
		GETUTCDATE() [FromDate],
		GETUTCDATE() [ToDate],
		0 [NumberDayOff],
		null [Note],
		0 Status,
		null [ReviewNote],
		0 TypeId,
		null TypeName,
		0 [UserId],
		0 [ReviewUserId],
		null [ReviewDate],
		0 [PeriodType],
		0 [RelatedUserId],
		0 [IsAlertCustomer],
		0 [HandoverUserId],
		null as UserName,
		null UserJobTitle,
		null UserAvatar,
		null UserGender,
		null ReviewUserName,
		null ReviewUserJobTitle,
		null ReviewUserAvatar,
		null ReviewUserGender,
		null RelatedUserIds
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