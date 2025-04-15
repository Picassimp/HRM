CREATE OR ALTER PROCEDURE [dbo].[Internal_PurchaseRequest_Manager_GetPaging](
    @countSkip INT, 
    @pageSize INT, 
    @managerId INT,
    @keyword NVARCHAR(250), 
    @userIds VARCHAR(50), 
    @departmentIds VARCHAR(50), 
    @projectIds VARCHAR(50), 
    @status VARCHAR(50), 
    @isUrgent VARCHAR(1)
)
AS
BEGIN
    ;WITH
     paging AS
         (
             SELECT
                 ROW_NUMBER() OVER (ORDER BY pr.CreatedDate DESC) AS RowCounts, 
				 pr.Id, 
				 pr.CreatedDate, 
				 pr.Name, 
				 SUM(prli.Quantity) AS Quantity, 
				 d.Name AS DepartmentName,
				 p.Name AS ProjectName, 
				 register.Id AS RegisterId, 
				 register.FullName AS RegisterName, 
				 pr.ReviewStatus, 
				 pr.IsUrgent,
				 pr.EstimateDate
             FROM dbo.PurchaseRequest pr
             JOIN dbo.PurchaseRequestLineItem prli ON prli.PurchaseRequestId = pr.Id
             JOIN dbo.UserInternal register ON register.Id = pr.UserId
             JOIN dbo.Department d ON pr.DepartmentId = d.Id
             LEFT JOIN dbo.Project p ON p.Id = pr.ProjectId
             JOIN dbo.UserInternal reviewer ON reviewer.Id = pr.ReviewUserId
             WHERE
                 pr.ReviewUserId = @managerId
                 AND (COALESCE(@isUrgent, '') = '' OR pr.IsUrgent = @isUrgent)
                 AND (COALESCE(@keyword, '') = '' OR pr.Name LIKE N'%' + TRIM(@keyword) + '%')
                 AND (COALESCE(@userIds, '') = '' OR pr.UserId IN (SELECT TRIM(value) FROM STRING_SPLIT(@userIds, ',')))
                 AND (COALESCE(@departmentIds, '') = '' OR d.Id IN (SELECT TRIM(value) FROM STRING_SPLIT(@departmentIds, ',')))
                 AND (COALESCE(@projectIds, '') = '' OR p.Id IN (SELECT TRIM(value) FROM STRING_SPLIT(@projectIds, ',')))
                 AND (COALESCE(@status, '') = '' OR pr.ReviewStatus IN (SELECT TRIM(value) FROM STRING_SPLIT(@status, ',')))
             GROUP BY
                 pr.Id, pr.CreatedDate, pr.Name, d.Name, p.Name, register.Id, register.FullName, pr.ReviewStatus, pr.IsUrgent, pr.EstimateDate
         )
    SELECT
        MAX(p.RowCounts) TotalRecord, 
		0 Id, 
		NULL CreatedDate, 
		NULL Name, 
		0 Quantity, 
		NULL DepartmentName, 
		NULL ProjectName, 
		0 RegisterId, 
		NULL RegisterName, 
		0 ReviewStatus, 
		0 IsUrgent,
		NULL EstimateDate
    FROM paging p
    UNION ALL
    SELECT
        *
    FROM paging p
    WHERE
        p.RowCounts > (@countSkip * @pageSize)
        AND p.RowCounts <= (@countSkip + 1) * @pageSize;
END;