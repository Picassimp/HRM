CREATE OR ALTER PROCEDURE [dbo].[Internal_ProjectFavorite_GetByUserId](@userId INT)
AS
BEGIN
	SELECT 
		c.Company AS ClientName,
		p.Id AS ProjectId,
		p.Name AS ProjectName,
		pf.Id AS ProjectFavoriteId
    FROM dbo.project p 
	JOIN dbo.Client c ON c.Id = p.ClientId
    JOIN dbo.ProjectMember pm ON pm.ProjectId = p.Id
	LEFT JOIN dbo.ProjectFavorite pf ON pf.ProjectId = p.Id AND pf.UserId = pm.UserInternalId
    WHERE P.IsDeleted = 0
        AND p.IsActive = 1
        AND pm.UserInternalId = @userId
        AND pm.IsActive = 1
		AND PM.IsDeleted = 0
	ORDER BY c.Company ASC, p.Name ASC
END