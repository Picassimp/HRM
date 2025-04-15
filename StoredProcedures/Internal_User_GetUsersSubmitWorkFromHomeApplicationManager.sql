CREATE OR ALTER PROCEDURE [dbo].[Internal_User_GetUsersSubmitWorkFromHomeApplicationManager](@managerId INT)
AS
BEGIN
    SELECT DISTINCT
           u.Id, 
           u.Name, 
           u.Email, 
           u.FullName, 
           u.JobTitle
    FROM dbo.UserInternal u
    JOIN dbo.WorkFromHomeApplication wfh ON u.Id = wfh.UserId
    WHERE
        wfh.ReviewUserId = @managerId;
END;