CREATE OR ALTER PROCEDURE [dbo].[Internal_Project_GetProjectStage]
(
	@projectId INT
)
AS
BEGIN
	SELECT 
		ps.Id,
		ps.Name,
		ps.WorkingHour,
		ps.StartDate,
		ps.EndDate,
		ps.Status,
		ps.Critical,
		ps.Highlight,
		ps.Note,
		COUNT(psm.Id) AS NumberOfMembers,
		STRING_AGG(psm.UserId, ',') AS ProjectStageMemberIds,
		STRING_AGG(ui.FullName, ', ') AS ProjectStageMemberNames,
		ISNULL(temp.StageWorkingMinute, 0) / 60.0 AS StageWorkingHour,
		ps.WorkingHour - ISNULL(temp.StageWorkingMinute, 0) / 60.0 AS StageWorkingHourRemaining
	FROM dbo.ProjectStage ps
	LEFT JOIN dbo.ProjectStageMember psm ON psm.ProjectStageId = ps.Id
	LEFT JOIN dbo.UserInternal ui ON ui.Id = psm.UserId
	LEFT JOIN (
		SELECT 
			ptlt.ProjectStageId,
			SUM(DATEDIFF(MINUTE, ptlt.StartTime, ISNULL(ptlt.StopTime, DATEADD(HOUR, 7, GETUTCDATE())))) AS StageWorkingMinute
		FROM dbo.Project p 
		JOIN dbo.ProjectTimeSheet pst ON p.id = pst.ProjectId
		JOIN dbo.ProjectTimeSheetLogTime ptlt ON pst.id = ptlt.ProjectTimeSheetId
		WHERE p.Id = @projectId AND ptlt.ProjectStageId IS NOT NULL
		GROUP BY ptlt.ProjectStageId
	) AS temp ON temp.ProjectStageId = ps.Id
	WHERE ps.ProjectId = @projectId
	GROUP BY
		ps.Id,
		ps.Name,
		ps.WorkingHour,
		ps.StartDate,
		ps.EndDate,
		ps.Status,
		ps.Critical,
		ps.Highlight,
		ps.Note,
		temp.StageWorkingMinute
	ORDER BY ps.StartDate ASC
END;