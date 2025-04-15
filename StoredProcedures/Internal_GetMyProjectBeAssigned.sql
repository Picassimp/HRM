CREATE OR ALTER PROCEDURE [dbo].[Internal_GetMyProjectBeAssigned](@userId INT)
AS
BEGIN
    SELECT DISTINCT
           c.Company AS ClientName, p.Id, p.Name AS ProjectName, p.Integration
    FROM Client c
    JOIN project p ON p.ClientId = c.Id
    JOIN ProjectMember pm ON pm.ProjectId = p.Id
    WHERE
        c.IsActive = 1
        AND p.IsActive = 1
        AND pm.UserInternalId = @userId
        AND pm.IsActive = 1;
END;