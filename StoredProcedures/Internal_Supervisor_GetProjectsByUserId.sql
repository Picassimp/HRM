CREATE OR ALTER PROCEDURE [dbo].[Internal_Supervisor_GetProjectsByUserId]
(
	@userId INT
)
AS
BEGIN	
	SELECT 
		p.Id, 
		p.Name, 
		p.Integration
	FROM dbo.ProjectMember pm
	JOIN dbo.Project p ON p.Id = pm.ProjectId
	WHERE
		pm.UserInternalId = @userId 
		AND p.IsActive = 1 
		AND p.IsDeleted = 0
		AND pm.IsActive = 1
		AND pm.IsDeleted = 0
END