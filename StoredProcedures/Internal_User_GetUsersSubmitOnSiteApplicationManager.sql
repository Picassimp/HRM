CREATE OR ALTER PROCEDURE [dbo].[Internal_User_GetUsersSubmitOnSiteApplicationManager](@managerId INT)
AS
BEGIN
    SELECT DISTINCT
           u.Id, 
           u.Name, 
           u.Email, 
           u.FullName, 
           u.JobTitle
    FROM dbo.UserInternal u
    JOIN dbo.OnsiteApplication oa ON u.Id = oa.UserId
    WHERE
        oa.ReviewUserId = @managerId;
END;