CREATE OR ALTER PROCEDURE [dbo].[Internal_Manager_Report_Timesheet](
@managerId INT, 
@startDate DATETIME, 
@endDate DATETIME, 
@companyIds VARCHAR(50), 
@projectIds VARCHAR(50), 
@userIds VARCHAR(100), 
@groupBy VARCHAR(100),
@projectStageIds VARCHAR(100),
@issueTypes VARCHAR(1000),
@tags VARCHAR(1000)
)
AS
BEGIN
    DECLARE @sql NVARCHAR(MAX);
    SELECT
        c.Company, 
        c.Name AS CustomerName, 
        p.Name AS ProjectName, 
        d.Name AS TeamName, 
        pm.UserInternalId AS UserId, 
        ui.FullName AS UserName, 
        pts.TaskId, 
        pts.Description, 
        pts.CreatedDate,
        DATEDIFF(MINUTE, plt.StartTime, plt.StopTime) AS WorkingTime,
		ps.Name AS ProjectStageName,
		plt.IssueType,
		STRING_AGG(ptt.Tag, ',') AS Tags
    INTO
        #tmp
    FROM dbo.Project p
    JOIN dbo.Client c ON c.Id = p.ClientId
    JOIN dbo.ProjectMember pm ON pm.ProjectId = p.Id
    JOIN dbo.UserInternal ui ON ui.Id = pm.UserInternalId
    JOIN dbo.Department d ON d.Id = ui.DepartmentId
    JOIN dbo.ProjectTimeSheet pts ON pts.ProjectId = p.Id
                                 AND pts.ProjectMemberId = pm.Id
    JOIN dbo.ProjectTimesheetLogTime plt ON plt.ProjectTimesheetId = pts.Id
    LEFT JOIN dbo.ProjectStage ps ON ps.ProjectId = p.Id AND plt.ProjectStageId = ps.Id
	LEFT JOIN dbo.ProjectTimesheetTag ptt ON ptt.ProjectTimesheetId = pts.Id
    WHERE
        p.Id IN
            (
                SELECT
                    p.Id
                FROM Project p
                JOIN ProjectMember pm ON pm.ProjectId = p.Id
                WHERE
                    (
                        pm.UserInternalId = @managerId
                        AND
                            (
                                pm.Role = 1
                                OR pm.Role = 7
                            )
                        AND pm.IsActive = 1
                        AND pm.IsDeleted = 0
                    )
            )
        AND CONVERT(DATE, pts.CreatedDate) >= CONVERT(DATE, @startDate)
        AND CONVERT(DATE, pts.CreatedDate) <= CONVERT(DATE, @endDate)
        AND
            (
                p.Id IN
                    (
                        SELECT
                            value
                        FROM STRING_SPLIT(@projectIds, ',')
                    )
                OR COALESCE(@projectIds, '') = ''
            ) -- filter by project Id
        AND
            (
                p.ClientId IN
                    (
                        SELECT
                            value
                        FROM STRING_SPLIT(@companyIds, ',')
                    )
                OR COALESCE(@companyIds, '') = ''
            ) -- filter by client Id
        AND
            (
                pm.UserInternalId IN
                    (
                        SELECT
                            value
                        FROM STRING_SPLIT(@userIds, ',')
                    )
                OR COALESCE(@userIds, '') = ''
            ) -- filter by user Id
		AND
            (
                ps.Id IN
                    (
                        SELECT
                            value
                        FROM STRING_SPLIT(@projectStageIds, ',')
                    )
                OR COALESCE(@projectStageIds, '') = ''
            ) -- filter by project Stage id
		AND
            (
                plt.IssueType IN (SELECT value FROM STRING_SPLIT(@issueTypes, ','))
                OR COALESCE(@issueTypes, '') = ''
            ) -- filter by Issue Type
	GROUP BY c.Company, 
        c.Name, 
        p.Name, 
        d.Name, 
        pm.UserInternalId, 
        ui.FullName, 
        pts.TaskId, 
        pts.Description, 
		plt.StartTime, 
		plt.StopTime,
        pts.CreatedDate,
		ps.Name,
		plt.IssueType
	HAVING
			EXISTS
			(
				SELECT 1
				FROM STRING_SPLIT(@tags, ',') AS splitTags
				WHERE CHARINDEX(',' + splitTags.value + ',', ',' + STRING_AGG(ptt.Tag, ',') + ',') > 0
			)
			OR COALESCE(@tags, '') = '' -- filter by Tag
    SET @sql = N'select ' + @groupBy + N', SUM(WorkingTime) as WorkingTime from #tmp
    group by ' + @groupBy + N' order by ' + @groupBy;
    EXECUTE sp_executesql @sql;
    DROP TABLE #tmp;
END;

--declare @managerId int = 38
--declare @startDate datetime = '03/01/2023'
--declare @endDate datetime = '03/27/2023'
--declare @companyIds varchar(20) = ''
--declare @projectIds varchar(20) = ''
--declare @userIds varchar(20) = ''
--declare @groupby varchar (100) ='ProjectName'
--exec [Manager_Report_Timesheet] @managerId,@startDate,@endDate,@companyIds,@projectIds,@userIds,@groupby
