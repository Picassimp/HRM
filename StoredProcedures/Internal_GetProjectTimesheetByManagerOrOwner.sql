CREATE OR ALTER PROCEDURE [dbo].[Internal_GetProjectTimesheetByManagerOrOwner](@projectId INT, @userId INT, @startDate DATETIME, @endDate DATETIME)
AS
BEGIN
    DECLARE @WHERE_SQL NVARCHAR(MAX);
    DECLARE @ParamDefinition NVARCHAR(MAX);
    DECLARE @SEARCH_SQL NVARCHAR(MAX);
    SET @WHERE_SQL = N' WHERE 1 = 1 ';
    IF @projectId IS NOT NULL
    BEGIN
        SET @WHERE_SQL = @WHERE_SQL + N' and p.Id = ' + CONVERT(NVARCHAR(MAX), @projectId);
    END;
    IF @userId IS NOT NULL
    BEGIN
        SET @WHERE_SQL = @WHERE_SQL + N' and pm.UserInternalId = ' + CONVERT(NVARCHAR(MAX), @userId);
    END;
    IF @startDate IS NOT NULL
       AND @endDate IS NOT NULL
    BEGIN
        SET @WHERE_SQL = @WHERE_SQL + N' and (convert(date, pt.CreatedDate) <=''' + CONVERT(NVARCHAR(100), @endDate) + N''' and convert(date, pt.CreatedDate) >=''' + CONVERT(NVARCHAR(100), @startDate) + N''')';
    END;
    SET @SEARCH_SQL = N'
	Select	pt.Id,
			pm.UserInternalId as UserId, 
			ui.FullName as UserName, 
            pt.TaskId, 
			pt.Description, 
			Sum(DATEDIFF(MINUTE, ptl.StartTime, ptl.StopTime)) AS WorkingTime,
			pt.CreatedDate
    from Project p
    join ProjectMember pm on pm.ProjectId = p.Id
    join ProjectTimeSheet pt on pt.ProjectMemberId = pm.Id
    join UserInternal ui on ui.Id = pm.UserInternalId
	join ProjectTimesheetLogTime ptl on ptl.ProjectTimesheetId = pt.Id' + @WHERE_SQL + N'group by pm.UserInternalid, ui.FullName, pt.Id, pt.TaskId, pt.Description, pt.CreatedDate, ptl.ProjectTimesheetId
	order by pt.CreatedDate';
    SET @ParamDefinition = N'@projectId_in INT,
							@userId_in INT,
							@startDate_in Datetime,
							@endDate_in Datetime';
    -- PRINT @SEARCH_SQL
    EXECUTE sp_executesql
        @SEARCH_SQL, @ParamDefinition, @projectId_in = @projectId, @userId_in = @userId, @startDate_in = @startDate, @endDate_in = @endDate;
END;