CREATE OR ALTER PROCEDURE [dbo].[Internal_ManagerGetDataProjects](@userId INT)
AS
BEGIN
    SELECT
        c.Company AS ClientName, p.Id, p.Name AS ProjectName, p.Integration
    FROM dbo.Client c
    JOIN dbo.Project p ON c.Id = p.ClientId
    JOIN dbo.ProjectMember pm ON p.Id = pm.ProjectId
    WHERE
        c.IsActive = 1
        AND p.IsActive = 1
        AND p.IsDeleted = 0
        AND pm.UserInternalId = @userId
        AND
            (
                pm.Role = 1
                OR pm.Role = 7
            )
		AND pm.IsActive = 1 AND pm.IsDeleted = 0
    ORDER BY
        ClientName;
END;
