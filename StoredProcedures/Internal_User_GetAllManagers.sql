CREATE OR ALTER PROCEDURE [dbo].[Internal_User_GetAllManagers]
AS
BEGIN
    SELECT DISTINCT
		ui.Id,
        ui.FullName
    FROM dbo.Department d
    JOIN dbo.OwnerOfDepartment od ON d.Id = od.DepartmentId
    JOIN dbo.UserInternal ui ON ui.Id = od.UserId
	WHERE ui.IsDeleted = 0 AND ui.HasLeft = 0
END;