CREATE OR ALTER PROCEDURE [dbo].[Internal_GetPurchaseRequestPaging](
@countSkip INT, 
@pageSize INT, 
@keyword NVARCHAR(MAX), 
@userIds VARCHAR(50), 
@departmentIds VARCHAR(50), 
@projectIds VARCHAR(50), 
@status VARCHAR(50), 
@isUrgent CHAR(1))
AS
BEGIN
	DECLARE @pending INT = 0
	DECLARE @managerRejectStatus INT = 1
	DECLARE @managerUpdateRequest INT = 5
	DECLARE @lackReceivedStatus INT = 5
	DECLARE @fullReceivedStatus INT = 4
	DECLARE @rejectStatus INT = 3
	DECLARE @cancelStatus INT = 4
	DECLARE @purchaseOrderRejectStatus INT = 6
    SELECT
        @keyword = TRIM(@keyword);
    -- UserFilter
    SELECT
        userTmp.UserId
    INTO
        #UserFilter
    FROM
        (
            SELECT
                TRIM(value) AS [UserId]
            FROM STRING_SPLIT(@userIds, ',')
        ) AS userTmp;
    -- DepartmentFilter
    SELECT
        departmentTmp.DepartmentId
    INTO
        #DepartmentFilter
    FROM
        (
            SELECT
                TRIM(value) AS [DepartmentId]
            FROM STRING_SPLIT(@departmentIds, ',')
        ) AS departmentTmp;
    -- ProjectFilter
    SELECT
        projectTmp.ProjectId
    INTO
        #ProjectFilter
    FROM
        (
            SELECT
                TRIM(value) AS [ProjectId]
            FROM STRING_SPLIT(@projectIds, ',')
        ) AS projectTmp;
    --StatusFilter
    SELECT
        statusTmp.Status
    INTO
        #StatusFilter
    FROM
        (
            SELECT
                TRIM(value) AS [Status]
            FROM STRING_SPLIT(@status, ',')
        ) AS statusTmp;
    --Filter All
    SELECT
        ROW_NUMBER() OVER (ORDER BY
                               pr.CreatedDate DESC
                          ) AS RowCounts, pr.Id
    INTO
        #PurchaseRequestFilter
    FROM dbo.PurchaseRequest pr
    JOIN dbo.UserInternal ui ON ui.Id = pr.UserId
    WHERE
        (
            COALESCE(@keyword, '') = ''
            OR pr.Name LIKE N'%' + @keyword + '%'
        )
		AND (pr.ReviewStatus NOT IN (@pending,@managerUpdateRequest,@managerRejectStatus))
        AND
            (
                ISNULL(@userIds, '') = ''
                OR EXISTS
        (
            SELECT
                1
            FROM #UserFilter uf
            WHERE
                uf.UserId = pr.UserId
        )
            )
        AND
            (
                ISNULL(@departmentIds, '') = ''
                OR EXISTS
        (
            SELECT
                1
            FROM #DepartmentFilter df
            WHERE
                df.DepartmentId = ui.DepartmentId
        )
            )
        AND
            (
                ISNULL(@projectIds, '') = ''
                OR EXISTS
        (
            SELECT
                1
            FROM #ProjectFilter pf
            WHERE
                pf.ProjectId = pr.ProjectId
        )
            )
        AND
            (
                ISNULL(@status, '') = ''
                OR EXISTS
        (
            SELECT
                1
            FROM #StatusFilter sf
            WHERE
                sf.Status = pr.ReviewStatus
        )
            )
        AND
            (
                ISNULL(@isUrgent,'') = ''
                OR pr.IsUrgent = CAST(@isUrgent AS BIT) 
            );

    --Paging
    DECLARE @totalRecord BIGINT =
                (
                    SELECT
                        MAX(prf.RowCounts) AS TotalRecord
                    FROM #PurchaseRequestFilter prf
                );
    SELECT
        prf.Id
    INTO
        #PurchaseRequestPaging
    FROM #PurchaseRequestFilter prf
    WHERE
        prf.RowCounts > (@countSkip * @pageSize)
        AND prf.RowCounts <= ((@countSkip + 1) * @pageSize);
    --Lấy số lượng từ PO
	SELECT tmp.PurchaseRequestId,STRING_AGG(tmp.FromPO,',') AS FromPO,SUM(tmp.QuantityFromPO) AS QuantityFromPO
	INTO
        #FromPurchaseRequest
	FROM(
    SELECT
        prli.PurchaseRequestId, STRING_AGG(CAST(pd.PurchaseOrderId AS VARCHAR(20)), ',') AS FromPO,
		CASE WHEN pli.IsReceived = 1 THEN SUM(pli.QuantityReceived)
		ELSE SUM(pli.Quantity) END AS QuantityFromPO
    FROM dbo.POPRLineItem pli
    JOIN dbo.PODetail pd ON pli.PurchaseOrderDetailId = pd.Id
	JOIN dbo.PurchaseOrder po ON pd.PurchaseOrderId = po.Id
    JOIN dbo.PurchaseRequestLineItem prli ON prli.Id = pli.PORequestLineItemId
    WHERE
		po.Status <> @purchaseOrderRejectStatus
		AND
        prli.PurchaseRequestId IN
            (
                SELECT
                    prp.Id
                FROM #PurchaseRequestPaging prp
            )
    GROUP BY
        prli.PurchaseRequestId,pli.IsReceived) AS tmp
	GROUP BY(tmp.PurchaseRequestId);
    --Lấy số lượng từ Phiếu xuất
    SELECT
        prli.PurchaseRequestId, STRING_AGG(CAST(ebd.ExportBillId AS VARCHAR(20)), ',') AS FromEB, SUM(ebli.Quantity) AS QuantityFromEB
    INTO
        #FromExportBill
    FROM dbo.PurchaseRequestLineItem prli
    JOIN dbo.ExportBillLineItem ebli ON ebli.PORequestLineItemId = prli.Id
    JOIN dbo.ExportBillDetail ebd ON ebli.ExportBillDetailId = ebd.Id
	JOIN dbo.ExportBill eb ON ebd.ExportBillId = eb.Id
    WHERE
		(eb.Status <> @rejectStatus AND eb.Status <>@cancelStatus)
		AND
        prli.PurchaseRequestId IN
            (
                SELECT
                    prp.Id
                FROM #PurchaseRequestPaging prp
            )
    GROUP BY
        prli.PurchaseRequestId;
    --Lấy thông tin từ request
    SELECT
        pr.Id, pr.CreatedDate, pr.Name, SUM(prli.Quantity) AS TotalRequestQuantity, ui.FullName, pr.ReviewStatus, d.Name AS DepartmentName, p.Name AS ProjectName, pr.IsUrgent,pr.EstimateDate
    INTO
        #Request
    FROM dbo.PurchaseRequest pr
    JOIN dbo.PurchaseRequestLineItem prli ON pr.Id = prli.PurchaseRequestId
    LEFT JOIN dbo.Project p ON p.Id = pr.ProjectId
    JOIN dbo.UserInternal ui ON ui.Id = pr.UserId
    JOIN dbo.Department d ON d.Id = pr.DepartmentId
    WHERE
        pr.Id IN
            (
                SELECT
                    prp.Id
                FROM #PurchaseRequestPaging prp
            )
    GROUP BY
        pr.Id, pr.CreatedDate, pr.Name, prli.PurchaseRequestId, ui.FullName, pr.ReviewStatus, d.Name, p.Name, pr.IsUrgent,pr.EstimateDate;
    SELECT
        r.Id, r.Name, r.CreatedDate, r.TotalRequestQuantity, fpr.FromPO, fpr.QuantityFromPO, 
		feb.FromEB, feb.QuantityFromEB, r.DepartmentName, r.ProjectName, r.FullName, r.ReviewStatus, 
		r.IsUrgent,r.EstimateDate, @totalRecord AS TotalRecord
    FROM #Request r
    LEFT JOIN #FromPurchaseRequest fpr ON r.Id = fpr.PurchaseRequestId
    LEFT JOIN #FromExportBill feb ON r.Id = feb.PurchaseRequestId
    JOIN #PurchaseRequestPaging prp ON r.Id = prp.Id
	ORDER BY r.CreatedDate DESC;
END