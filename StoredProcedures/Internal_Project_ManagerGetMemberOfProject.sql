CREATE OR ALTER PROCEDURE [dbo].[Internal_Project_ManagerGetMemberOfProject](@userId INT)
AS
BEGIN
    SELECT DISTINCT
           pm.UserInternalId AS Id, ui.FullName AS UserName
    FROM Project p
    JOIN ProjectMember pm ON pm.ProjectId = p.Id
    JOIN UserInternal ui ON ui.Id = pm.UserInternalId
    WHERE
        p.Id IN
            (
                SELECT
                    p.Id
                FROM Project p
                JOIN ProjectMember pm ON pm.ProjectId = p.Id
                WHERE
                    (
                        pm.UserInternalId = @userId
                        AND
                            (
                                pm.Role = 1
                                OR pm.Role = 7
                            )
                    )
            );
END;