CREATE OR ALTER PROCEDURE [dbo].[Internal_Project_ManagerGetDataDetailForReporting](
@managerId INT, 
@startDate DATETIME, 
@endDate DATETIME, 
@companyIds VARCHAR(50), 
@projectIds VARCHAR(50), 
@userIds VARCHAR(100),
@projectStageIds VARCHAR(100),
@issueTypes VARCHAR(1000),
@tags VARCHAR(1000))
AS
BEGIN
    SELECT 
        p.Name AS Project, c.Company, ui.FullName AS [User], ui.Email, d.Name AS Team, pts.TaskId, pts.Description, ptl.StartTime, ptl.StopTime AS EndTime, DATEDIFF(MINUTE, ptl.StartTime, ptl.StopTime) AS WorkingTime, ps.Name AS 'ProjectStageName', ptl.IssueType, STRING_AGG(ptt.Tag, ',') AS Tags
    FROM dbo.Project p
    JOIN dbo.Client c ON c.Id = p.ClientId
    JOIN dbo.ProjectMember pm ON pm.ProjectId = p.Id
    JOIN dbo.UserInternal ui ON ui.Id = pm.UserInternalId
    JOIN dbo.Department d ON d.Id = ui.DepartmentId
    JOIN dbo.ProjectTimeSheet pts ON(
                                    pts.ProjectId = p.Id
                                    AND pts.ProjectMemberId = pm.Id
                                )
    JOIN dbo.ProjectTimesheetLogTime ptl ON ptl.ProjectTimesheetId = pts.Id
	LEFT JOIN dbo.ProjectStage ps ON ps.ProjectId = p.Id AND ps.Id = ptl.ProjectStageId
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
			) -- filter by project Stage Id
		AND
			(
				ptl.IssueType IN (SELECT value FROM STRING_SPLIT(@issueTypes, ','))
				OR COALESCE(@issueTypes, '') = ''
			) -- filter by Issue Type
	GROUP BY p.Name, c.Company, ui.FullName , ui.Email, d.Name, pts.TaskId, pts.Description, ptl.StartTime, ptl.StopTime, ps.Name , ptl.IssueType
	HAVING
			EXISTS
			(
				SELECT 1
				FROM STRING_SPLIT(@tags, ',') AS splitTags
				WHERE CHARINDEX(',' + splitTags.value + ',', ',' + STRING_AGG(ptt.Tag, ',') + ',') > 0
			)
			OR COALESCE(@tags, '') = '' -- filter by Tag
END;