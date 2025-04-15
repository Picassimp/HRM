CREATE OR ALTER PROCEDURE [dbo].[Internal_User_GetAll]
AS
BEGIN
    SELECT  ui.Id, 
			ui.Name, 
			ui.Email, 
			ui.FullName, 
			ui.JobTitle, 
			ui.OffDay, 
			ui.YearOffDay, 
			ui.HasLeft, 
			ui.AcceptOfferDate, 
			d.Name AS DepartmentName, 
			j.Id AS JobId, 
			j.Name AS JobName, 
			l.Id AS LevelId, 
			l.Name AS LevelName, 
			ui.Avatar, 
			ui.Gender
    FROM dbo.UserInternal AS ui
    JOIN dbo.Department AS d ON ui.DepartmentId = d.Id
    LEFT JOIN dbo.Job AS j ON ui.JobId = j.Id
    LEFT JOIN dbo.Level AS l ON ui.LevelId = l.Id
    WHERE ui.IsDeleted = 0 AND ui.HasLeft = 0
    ORDER BY ui.FullName;
END;