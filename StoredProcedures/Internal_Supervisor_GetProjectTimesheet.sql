CREATE OR ALTER PROCEDURE [dbo].[Internal_Supervisor_GetProjectTimesheet]
(
	@projectId INT,
	@userIds VARCHAR(MAX),
	@startDate DATETIME, 
	@endDate DATETIME
)
AS
BEGIN
	SELECT 
		pm.UserInternalId AS UserId,
		ui.FullName AS UserName,
		p.Id AS ProjectId,
		p.Name AS ProjectName,
		SUM(DATEDIFF(MINUTE, ptlt.StartTime, ptlt.StopTime)) AS WorkingTime,
		pts.CreatedDate
	FROM dbo.Project p
	JOIN dbo.ProjectMember pm ON pm.ProjectId = p.Id
	JOIN dbo.ProjectTimeSheet pts ON pts.ProjectMemberId = pm.Id
	JOIN dbo.UserInternal ui ON ui.Id = pm.UserInternalId
	JOIN dbo.ProjectTimesheetLogTime ptlt ON ptlt.ProjectTimesheetId = pts.Id
	WHERE 
		(ISNULL(@projectId, '') = '' OR p.Id = @projectId) AND
		pm.UserInternalId IN (SELECT value FROM STRING_SPLIT(@userIds, ','))
		AND (CAST(pts.CreatedDate AS DATE) >= (CAST(@startDate AS DATE)) 
			AND (CAST(pts.CreatedDate AS DATE)) <= (CAST(@endDate AS DATE)))
	GROUP BY 
		pm.UserInternalId, 
		ui.FullName,
		p.Id,
		p.Name,
		pts.CreatedDate
	ORDER BY
		pts.CreatedDate
END