CREATE OR ALTER PROCEDURE [dbo].[Internal_GetProjectTimesheetSelfByUser]
(
	@userId INT, 
	@projectId INT, 
	@clientId INT,
	@startDate DATETIME, 
	@endDate DATETIME
)
AS
BEGIN
    SELECT
        p.Id AS ProjectId,
        p.Name AS ProjectName,
        pts.Id, 
        pts.TaskId, 
        pts.Description, 
        pts.IssueType,
        pts.CreatedDate, 
        pts.ProcessStatus, 
		pte.EstimateTimeInSecond,
        ISNULL(SUM(DATEDIFF(MINUTE, ptl.StartTime, ISNULL(ptl.StopTime, DATEADD(HOUR, 7, GETDATE())))), 0) AS WorkingTime
    FROM dbo.Project p
    JOIN dbo.ProjectMember pm ON pm.ProjectId = p.Id
    JOIN dbo.ProjectTimeSheet pts ON pts.ProjectMemberId = pm.Id
    JOIN dbo.UserInternal ui ON ui.Id = pm.UserInternalId
    LEFT JOIN dbo.ProjectTimesheetLogTime ptl ON ptl.ProjectTimesheetId = pts.Id
	LEFT JOIN dbo.ProjectTimesheetEstimate pte ON pte.ProjectId = p.Id AND pte.TaskId = pts.TaskId
    WHERE
        pm.UserInternalId = @userId
        AND (COALESCE(@projectId, '') = '' OR pts.ProjectId = @projectId)
		AND (COALESCE(@clientId, '') = '' OR p.ClientId = @clientId)
        AND CONVERT(DATE, pts.CreatedDate) >= CONVERT(DATE, @startDate)
        AND CONVERT(DATE, pts.CreatedDate) <= CONVERT(DATE, @endDate)
    GROUP BY
        p.Name, 
		p.Id, 
		pts.Id, 
		pts.TaskId, 
		pts.Description, 
		pts.IssueType, 
		pts.CreatedDate, 
		pts.ProcessStatus, 
		ptl.ProjectTimesheetId,
		pte.EstimateTimeInSecond
    ORDER BY
        p.Name, pts.CreatedDate;
END;