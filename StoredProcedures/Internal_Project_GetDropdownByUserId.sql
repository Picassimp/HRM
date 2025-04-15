CREATE OR ALTER PROCEDURE [dbo].[Internal_Project_GetDropdownByUserId](@userId INT)
AS
BEGIN
    SELECT 
		p.Id, 
		c.Id AS ClientId,
		c.Company AS Name,
		p.Name AS ProjectName, 
		p.Integration
    FROM dbo.Client c
    JOIN dbo.Project p ON p.ClientId = c.Id
    JOIN dbo.ProjectMember pm ON pm.ProjectId = p.Id
	LEFT JOIN dbo.ProjectFavorite pf ON pf.ProjectId = p.Id AND pf.UserId = pm.UserInternalId
    WHERE
        c.IsActive = 1
        AND p.IsActive = 1
        AND pm.UserInternalId = @userId
        AND pm.IsActive = 1
	ORDER BY
		pf.Id DESC, p.Name
END;