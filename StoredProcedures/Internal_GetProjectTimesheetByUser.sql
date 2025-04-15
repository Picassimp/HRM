CREATE OR ALTER PROCEDURE [dbo].[Internal_GetProjectTimesheetByUser](@projectId INT, @userId INT, @startDate DATETIME, @endDate DATETIME)
AS
BEGIN
    SELECT
        pts.Id, 
		pts.TaskId, 
		pts.Description, 
		pts.IssueType,
		pts.CreatedDate, 
		pts.ProcessStatus, 
		ISNULL(SUM(DATEDIFF(MINUTE, ptlt.StartTime, ISNULL(ptlt.StopTime, DATEADD(HOUR, 7, GETDATE())))), 0) AS WorkingTime
    FROM dbo.Project p
    JOIN dbo.ProjectMember pm ON pm.ProjectId = p.Id
    JOIN dbo.ProjectTimeSheet pts ON pts.ProjectMemberId = pm.Id
    JOIN dbo.UserInternal ui ON ui.Id = pm.UserInternalId
    LEFT JOIN dbo.ProjectTimesheetLogTime ptlt ON ptlt.ProjectTimesheetId = pts.Id
    WHERE
        pm.UserInternalId = @userId
        AND p.Id = @projectId
        AND CONVERT(DATE, pts.CreatedDate) >= CONVERT(DATE, @startDate)
        AND CONVERT(DATE, pts.CreatedDate) <= CONVERT(DATE, @endDate)
    GROUP BY
        pts.Id, 
		pts.TaskId, 
		pts.Description, 
		pts.IssueType,
		pts.CreatedDate, 
		pts.ProcessStatus, 
		ptlt.ProjectTimesheetId
    ORDER BY
        pts.CreatedDate;
END;