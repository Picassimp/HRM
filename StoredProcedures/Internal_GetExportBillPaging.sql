CREATE OR ALTER PROCEDURE [dbo].[Internal_GetExportBillPaging](
@countSkip INT, 
@pageSize INT, 
@keyword VARCHAR(MAX), 
@userIds VARCHAR(50), 
@departmentIds VARCHAR(50), 
@projectIds VARCHAR(50), 
@status VARCHAR(50))
AS
BEGIN
    --UserFilter
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
                               eb.CreateDate DESC
                          ) AS RowCounts, eb.Id
    INTO
        #EBFilter
    FROM dbo.ExportBill eb 
    LEFT JOIN dbo.ExportBillDetail ebd ON ebd.ExportBillId = eb.Id
    LEFT JOIN dbo.ExportBillLineItem ebli ON ebli.ExportBillDetailId = ebd.Id
    LEFT JOIN dbo.PurchaseRequestLineItem prli ON ebli.PORequestLineItemId = prli.Id
    LEFT JOIN dbo.PurchaseRequest pr ON pr.Id = prli.PurchaseRequestId
    LEFT JOIN dbo.Project p ON pr.ProjectId = p.Id
    LEFT JOIN dbo.UserInternal ui ON ui.Id = pr.UserId
    LEFT JOIN dbo.Department d ON ui.DepartmentId = d.Id
    WHERE
        (
            ISNULL(@keyword, '') = ''
            OR eb.Id = CAST(@keyword AS INT) 
        )
        AND
            (
                ISNULL(@userIds, '') = ''
                OR EXISTS
        (
            SELECT
                1
            FROM #UserFilter uf 
            WHERE
                uf.UserId = eb.CreateUserId
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
                pf.ProjectId
            FROM #ProjectFilter pf
            WHERE
                pr.ProjectId = pf.ProjectId
        )
            )
        AND
            (
                ISNULL(@status, '') = ''
                OR EXISTS
        (
            SELECT
                *
            FROM #StatusFilter sf
            WHERE
                sf.Status = eb.Status
        )
            )
			GROUP BY  eb.Id,eb.CreateDate;
    --Paging EB
    DECLARE @totalRecord BIGINT =
                (
                    SELECT
                        MAX(ef.RowCounts)
                    FROM #EBFilter ef 
                );
    SELECT
        ef.Id
    INTO
        #EBPaging
    FROM #EBFilter ef 
    WHERE
        ef.RowCounts > (@countSkip * @pageSize)
        AND ef.RowCounts <= ((@countSkip + 1) * @pageSize);
    --Lấy thông tin EB
    SELECT DISTINCT
           eb.CreateDate, eb.Id, ui2.FullName, d.Name AS DepartmentName, p.Name ProjectName, pr.Id RequestId, pr.Name RequestName, eb.Status,eb.IsExport
    INTO
        #EBInfo
    FROM dbo.ExportBill eb 
	JOIN dbo.UserInternal ui2 ON eb.CreateUserId = ui2.Id
    LEFT JOIN dbo.ExportBillDetail ebd ON ebd.ExportBillId = eb.Id
    LEFT JOIN dbo.ExportBillLineItem ebli ON ebli.ExportBillDetailId = ebd.Id
    LEFT JOIN dbo.PurchaseRequestLineItem prli ON ebli.PORequestLineItemId = prli.Id
    LEFT JOIN dbo.PurchaseRequest pr ON pr.Id = prli.PurchaseRequestId
    LEFT JOIN dbo.Project p ON pr.ProjectId = p.Id
    LEFT JOIN dbo.UserInternal ui ON ui.Id = pr.UserId
    LEFT JOIN dbo.Department d ON ui.DepartmentId = d.Id
    WHERE
        eb.Id IN
            (
                SELECT
                    ep.Id
                FROM #EBPaging ep 
            );
    --Lấy tổng số lượng từng phiếu xuất
    SELECT
        ebd.ExportBillId,SUM(ebli.Quantity) AS TotalQuantity
    INTO
        #EBTotalQuantity
    FROM dbo.ExportBillDetail ebd
	JOIN dbo.ExportBillLineItem ebli ON ebd.Id = ebli.ExportBillDetailId
    WHERE
        ebd.ExportBillId IN
            (
                SELECT
                    ep.Id
                FROM #EBPaging ep 
            )
    GROUP BY(ebd.ExportBillId);
    SELECT
        ei.CreateDate, ei.Id, ei.FullName, STRING_AGG(ei.DepartmentName, ',') AS Departments, 
		STRING_AGG(ei.ProjectName, ',') AS Projects, etq.TotalQuantity, 
		STRING_AGG(CONCAT('{"StringId": "',COALESCE(ei.RequestName,''), '", "Id": ',COALESCE(ei.RequestId,0), '}'), ';') AS FromRequests, ei.Status,ei.IsExport,@totalRecord AS TotalRecord
    FROM #EBInfo ei 
    LEFT JOIN #EBTotalQuantity etq ON ei.Id = etq.ExportBillId
	GROUP BY ei.CreateDate,ei.Id,ei.FullName,ei.Status,ei.IsExport,etq.TotalQuantity
	ORDER BY ei.CreateDate DESC;
END;