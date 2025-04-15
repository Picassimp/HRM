CREATE OR ALTER PROCEDURE [dbo].[Internal_ChromeExtension_GetProjectTimesheet]
(
	@userId INT,
	@date DATETIME
)
AS
BEGIN
	SELECT 
		p.Id AS ProjectId,
		p.Name AS ProjectName,
		p.Integration,
		pts.Id AS ProjectTimesheetId,
		pts.ProcessStatus,
		pts.TaskId,
		pf.Id,
		ISNULL(SUM(DATEDIFF(MINUTE, ptlt.StartTime, ISNULL(ptlt.StopTime, DATEADD(HOUR, 7, GETUTCDATE())))), 0) AS WorkingTimeInMinute
	FROM dbo.Project p 
	JOIN dbo.ProjectMember pm ON pm.ProjectId = p.Id
	LEFT JOIN dbo.ProjectTimeSheet pts ON pts.ProjectId = p.Id AND pts.ProjectMemberId = pm.Id AND pts.CreatedDate = @date
	LEFT JOIN dbo.ProjectTimesheetLogTime ptlt ON ptlt.ProjectTimesheetId = pts.Id
	LEFT JOIN dbo.ProjectFavorite pf ON pf.ProjectId = p.Id AND pf.UserId = pm.UserInternalId
	WHERE 
		pm.IsActive = 1 
		AND pm.IsDeleted = 0 
		AND p.IsActive = 1
		AND p.IsDeleted = 0
		AND pm.UserInternalId = @userId
	GROUP BY 
		p.Id,
		p.Name,
		p.Integration,
		pts.Id,
		pts.ProcessStatus,
		pts.TaskId,
		pf.Id
	ORDER BY
		pf.Id DESC,
		p.Name
END