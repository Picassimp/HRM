CREATE OR ALTER PROCEDURE [dbo].[Internal_PurchaseRequest_GetProjectDropdown]
(
	@userIds VARCHAR(MAX)
)
AS
BEGIN	
	SELECT value AS userId
	INTO #userIds
	FROM STRING_SPLIT(@userIds, ',')

	SELECT DISTINCT p.Id, p.Name
	FROM dbo.Project p 
	JOIN dbo.ProjectMember pm ON pm.ProjectId = p.Id
	WHERE p.IsActive = 1 
		AND p.IsDeleted = 0
		AND pm.IsActive = 1
		AND pm.IsDeleted = 0
		AND EXISTS (SELECT 1 FROM #userIds WHERE pm.UserInternalId = #userIds.userId) 
	DROP TABLE #userIds
END