CREATE OR ALTER PROCEDURE [dbo].[Internal_Project_CalculateTotalWorkingHour]
(
	@projectId INT
)
AS
BEGIN
	SELECT 
		SUM(DATEDIFF(MINUTE, ptlt.StartTime, ISNULL(ptlt.StopTime, DATEADD(HOUR, 7, GETUTCDATE())))) / 60.0 AS TotalProjectWorkingHour
	FROM dbo.ProjectTimeSheet pts 
	JOIN dbo.ProjectTimesheetLogTime ptlt ON ptlt.ProjectTimesheetId = pts.Id
	WHERE pts.ProjectId = @projectId
END;