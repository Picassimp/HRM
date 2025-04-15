CREATE OR ALTER PROCEDURE [dbo].[Internal_User_GetByManagerId]
(
	@managerId INT
)
AS
BEGIN
    SELECT DISTINCT ui.Id, ui.Name, ui.Email, ui.FullName, ui.JobTitle
    FROM dbo.UserInternal ui
    JOIN dbo.LeaveApplication la ON ui.Id = la.UserId
    WHERE la.ReviewUserId = @managerId;
END;
