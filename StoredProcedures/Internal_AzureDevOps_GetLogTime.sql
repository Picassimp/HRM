CREATE OR ALTER PROCEDURE [dbo].[Internal_AzureDevOps_GetLogTime](@taskId VARCHAR(MAX), @projectId INT)
AS
BEGIN
    SELECT
        pm.DevOpsAccountEmail AS DevOpsEmail, ptlt.StartTime, ptlt.StopTime, ptlt.LogWorkId, ptlt.Comment, ptlt.TimeSpentSeconds AS TimeSpent
    FROM dbo.ProjectTimeSheet pts
    JOIN dbo.ProjectTimesheetLogTime ptlt ON ptlt.ProjectTimesheetId = pts.Id
    JOIN dbo.ProjectMember pm ON pts.ProjectMemberId = pm.Id
    JOIN dbo.UserInternal ui ON ui.Id = pm.UserInternalId
    WHERE
        pts.TaskId = @taskId
        AND pts.ProjectId = @projectId
        AND ptlt.LogWorkId IS NOT NULL
    ORDER BY
        pts.CreatedDate DESC, ptlt.StartTime DESC;
END;