CREATE OR ALTER PROCEDURE [dbo].[Internal_PurchaseRequest_User_GetPaging]
(
	@countSkip INT, 
	@pageSize INT, 
	@userId INT,
	@isUrgent VARCHAR(1)
)
AS
BEGIN	
	;WITH paging AS 
		(
			SELECT 
				ROW_NUMBER() OVER (ORDER BY pr.CreatedDate DESC) AS RowCounts, 
				pr.Id,
				pr.CreatedDate,
				pr.Name,
				SUM(prli.Quantity) AS Quantity,
				d.Name AS DepartmentName,
				p.Name AS ProjectName,
				reviewer.Id AS ReviewUserId,
				reviewer.FullName AS ReviewUserName,
				pr.ReviewStatus,
				pr.IsUrgent,
				pr.EstimateDate
			FROM dbo.PurchaseRequest pr
			JOIN dbo.PurchaseRequestLineItem prli ON prli.PurchaseRequestId = pr.Id
			JOIN dbo.UserInternal register ON register.Id = pr.UserId
			JOIN dbo.Department d ON pr.DepartmentId = d.Id
			LEFT JOIN dbo.Project p ON p.Id = pr.ProjectId
			JOIN dbo.UserInternal reviewer ON reviewer.Id = pr.ReviewUserId
			WHERE pr.UserId = @userId 
				AND (COALESCE(@isUrgent, '') = '' OR pr.IsUrgent = @isUrgent)
			GROUP BY
				pr.Id,
				pr.CreatedDate,
				pr.Name,
				d.Name,
				p.Name,
				reviewer.Id,
				reviewer.FullName,
				pr.ReviewStatus,
				pr.IsUrgent,
				pr.EstimateDate
		)
	SELECT MAX(p.RowCounts) TotalRecord, 
		0 Id, 
		NULL CreatedDate, 
		NULL Name,
		0 Quantity, 
		NULL DepartmentName,
		NULL ProjectName, 
		0 ReviewUserId, 
		NULL ReviewUserName, 
		0 ReviewStatus,
		0 IsUrgent,
		NULL EstimateDate
	FROM paging p
	UNION ALL
	SELECT *
	FROM paging p
	WHERE p.RowCounts > (@countSkip * @pageSize) AND p.RowCounts <= (@countSkip + 1) * @pageSize;
END