CREATE OR ALTER PROCEDURE [dbo].[Internal_Project_GetForSyncIssueType]
AS
BEGIN
	DECLARE @none INT = 0;
    SELECT
		p.Id, 
		p.Integration,
		p.JiraDomain, 
		p.JiraUser, 
		p.JiraKey, 
		p.AzureDevOpsProject,
		p.AzureDevOpsKey,
		p.AzureDevOpsOrganization
	FROM dbo.Project p
	WHERE 
		p.Integration <> @none
		AND p.IsActive = 1 
		AND p.IsDeleted = 0
END;