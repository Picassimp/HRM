CREATE OR ALTER PROCEDURE [dbo].[Internal_PurchaseRequest_Accountant_GetPaging]
(
	@countSkip INT, 
	@pageSize INT, 
	@keyword NVARCHAR(MAX),
	@userIds VARCHAR(MAX),
	@departmentIds VARCHAR(MAX),
	@projectIds VARCHAR(MAX),
	@statuses VARCHAR(MAX),
	@isUrgent VARCHAR(1),
	@isDirector BIT
)
AS
BEGIN

DECLARE 
		@accountantReject INT = 3,
		@directorRejected INT = 4,
		@accountantUpdateRequest INT = 7,
		@directorUpdateRequest INT = 8,
		@managerApproved INT = 9, 
		@accountantApproved INT = 11,
		@directorApproved INT = 12,
		@onPurchase INT = 13,
		@delivered INT = 14,
		@closed INT = 16;

SELECT @keyword = TRIM(@keyword);

SELECT value AS userId 
INTO #userIds
FROM STRING_SPLIT(@userIds, ',')

SELECT value AS departmentId 
INTO #departmentIds
FROM STRING_SPLIT(@departmentIds, ',')

SELECT value AS projectId 
INTO #projectIds
FROM STRING_SPLIT(@projectIds, ',')

SELECT value AS status 
INTO #statuses
FROM STRING_SPLIT(@statuses, ',')

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
			WHERE (COALESCE(@keyword, '') = '' OR pr.Name LIKE N'%' + @keyword + '%')
				AND (COALESCE(@userIds, '') = '' OR EXISTS(SELECT 1 FROM #userIds WHERE #userIds.userId = pr.UserId))
				AND (COALESCE(@departmentIds, '') = '' OR EXISTS(SELECT 1 FROM #departmentIds WHERE #departmentIds.departmentId = d.Id))
				AND (COALESCE(@projectIds, '') = '' OR EXISTS(SELECT 1 FROM #projectIds WHERE #projectIds.projectId = p.Id))
				AND (COALESCE(@statuses, '') = '' OR EXISTS(SELECT 1 FROM #statuses WHERE #statuses.status = pr.ReviewStatus))
				AND (COALESCE(@isUrgent, '') = '' OR pr.IsUrgent = @isUrgent)
				AND 
				(
					(@isDirector = 1 AND pr.ReviewStatus in (@directorRejected, @directorUpdateRequest, @accountantApproved, @directorApproved, @onPurchase, @delivered, @closed))
					OR
					(@isDirector = 0 AND pr.ReviewStatus in (@accountantReject, @accountantUpdateRequest, @managerApproved, @accountantApproved, @directorApproved, @onPurchase, @delivered, @closed))
				)
			GROUP BY
				pr.Id,
				pr.CreatedDate,
				pr.Name,
				d.Name,
				p.Name,
				register.Id,
				register.FullName,
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
		0 RegisterId, 
		NULL RegisterName, 
		0 ReviewStatus,
		0 IsUrgent,
		NULL EstimateDate
	FROM paging p
	UNION ALL
	SELECT *
	FROM paging p
	WHERE p.RowCounts > (@countSkip * @pageSize) AND p.RowCounts <= (@countSkip + 1) * @pageSize;

	DROP TABLE #userIds
	DROP TABLE #departmentIds
	DROP TABLE #projectIds
	DROP TABLE #statuses
END