CREATE OR ALTER PROCEDURE [dbo].[Internal_PurchaseRequest_IsValidMember_ByProjectIdAndUserId]
(
	@projectId INT, 
	@userId INT
)
AS
BEGIN	
	SELECT 
		p.Id
	FROM dbo.Project p
	JOIN dbo.ProjectMember pm ON pm.ProjectId = p.Id
	WHERE p.Id = @projectId AND pm.UserInternalId = @userId
	AND p.IsActive = 1 AND p.IsDeleted = 0
	AND pm.IsActive = 1 AND pm.IsDeleted = 0
END