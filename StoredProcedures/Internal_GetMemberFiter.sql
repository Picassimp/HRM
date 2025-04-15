CREATE OR ALTER PROCEDURE [dbo].[Internal_GetMemberFiter](@projectId INT)
AS
BEGIN
    SELECT
        pm.UserInternalId AS UserId, ui.FullName
    FROM Project p
    JOIN ProjectMember pm ON pm.ProjectId = p.Id
    JOIN UserInternal ui ON pm.UserInternalId = ui.Id
    WHERE
        p.Id = @projectId
        AND pm.IsActive = 1
        AND pm.IsDeleted = 0;
END;