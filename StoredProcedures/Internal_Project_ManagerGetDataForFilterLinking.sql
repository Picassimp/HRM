CREATE OR ALTER PROCEDURE [dbo].[Internal_Project_ManagerGetDataForFilterLinking](@userId INT)
AS
BEGIN
    SELECT DISTINCT
           ui.Id AS UserId, ui.FullName AS UserName, ui.DepartmentId AS TeamId, d.Name AS TeamName, p.Id AS ProjectId, p.Name AS ProjectName, p.ClientId AS CompanyId, c.Company AS CompanyName
    FROM UserInternal ui
    JOIN Department d ON d.Id = ui.DepartmentId
    JOIN ProjectMember pm ON pm.UserInternalId = ui.Id
    JOIN Project p ON p.Id = pm.ProjectId
    LEFT JOIN Client c ON c.Id = p.ClientId
    WHERE
        ui.HasLeft = 0
        AND ui.IsDeleted = 0
        AND p.Id IN
                (
                    SELECT
                        p.Id
                    FROM Project p
                    JOIN ProjectMember pm ON p.Id = pm.ProjectId
                    WHERE
                        pm.UserInternalId = @userId
                        AND
                            (
                                pm.Role = 1
                                OR pm.Role = 7
                            )
                );
END;