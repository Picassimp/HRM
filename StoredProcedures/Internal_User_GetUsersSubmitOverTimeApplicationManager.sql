CREATE OR ALTER PROCEDURE [dbo].[Internal_User_GetUsersSubmitOverTimeApplicationManager](@managerId INT)
AS
BEGIN
    SELECT DISTINCT
           u.Id, 
           u.Name, 
           u.Email, 
           u.FullName, 
           u.JobTitle
    FROM dbo.UserInternal u
    JOIN dbo.OverTimeApplication oa ON u.Id = oa.UserId
    WHERE
        oa.ReviewUserId = @managerId;
END;