CREATE OR ALTER PROCEDURE [dbo].[Internal_GetPurchaseOrderPaging]
(@countSkip INT, 
@pageSize INT,
@keyword VARCHAR(MAX), 
@vendorIds VARCHAR(50),
@departmentIds VARCHAR(50),
@projectIds VARCHAR(50),
@status VARCHAR(50),
@isCompensationPO BIT = 0)
AS
BEGIN
    DECLARE @lackReceiveStatus INT = 5;
    --VendorFilter
    SELECT
        vendorTmp.VendorId
    INTO
        #VendorFilter
    FROM
        (
            SELECT
                TRIM(value) AS [VendorId]
            FROM STRING_SPLIT(@vendorIds, ',')
        ) AS vendorTmp;
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
                               po.CreateDate DESC
                          ) AS RowCounts, po.Id
    INTO
        #POFilter
    FROM dbo.PurchaseOrder po
    LEFT JOIN dbo.Vendor v ON v.Id = po.Id
    LEFT JOIN dbo.PODetail pd ON po.Id = pd.PurchaseOrderId
    LEFT JOIN dbo.POPRLineItem pli ON pli.PurchaseOrderDetailId = pd.Id
    LEFT JOIN dbo.PurchaseRequestLineItem prli ON pli.PORequestLineItemId = prli.Id
    LEFT JOIN dbo.PurchaseRequest pr ON pr.Id = prli.PurchaseRequestId
    LEFT JOIN dbo.Project p ON pr.ProjectId = p.Id
    LEFT JOIN dbo.UserInternal ui ON ui.Id = pr.UserId
    LEFT JOIN dbo.Department d ON ui.DepartmentId = d.Id
    WHERE
        (
            ISNULL(@keyword, '') = ''
            OR po.Id = CAST(@keyword AS INT)
        )
        AND
            (
                ISNULL(@vendorIds, '') = ''
                OR EXISTS
        (
            SELECT
                1
            FROM #VendorFilter vf
            WHERE
                vf.VendorId = po.VendorId
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
                sf.Status = po.Status
        )
            )
        AND
            (
                @isCompensationPO = 0
                OR po.IsCompensationPO = @isCompensationPO
            )
    GROUP BY
        po.Id, po.CreateDate;
    --Paging PO 
    DECLARE @totalRecord BIGINT =
                (
                    SELECT
                        MAX(pf.RowCounts)
                    FROM #POFilter pf
                );
    SELECT
        pf.Id
    INTO
        #POPaging
    FROM #POFilter pf
    WHERE
        pf.RowCounts > (@countSkip * @pageSize)
        AND pf.RowCounts <= ((@countSkip + 1) * @pageSize);
    --Lấy thông tin PO
    SELECT DISTINCT
           po.CreateDate, po.Id, v.VendorName, d.Name AS DepartmentName, p.Name ProjectName, pr.Id RequestId, pr.Name RequestName, po.Status, po.IsCompensationPO
    INTO
        #POInfo
    FROM dbo.PurchaseOrder po
    LEFT JOIN dbo.Vendor v ON v.Id = po.VendorId
    LEFT JOIN dbo.PODetail pd ON po.Id = pd.PurchaseOrderId
    LEFT JOIN dbo.POPRLineItem pli ON pli.PurchaseOrderDetailId = pd.Id
    LEFT JOIN dbo.PurchaseRequestLineItem prli ON pli.PORequestLineItemId = prli.Id
    LEFT JOIN dbo.PurchaseRequest pr ON pr.Id = prli.PurchaseRequestId
    LEFT JOIN dbo.Project p ON pr.ProjectId = p.Id
    LEFT JOIN dbo.UserInternal ui ON ui.Id = pr.UserId
    LEFT JOIN dbo.Department d ON ui.DepartmentId = d.Id
    WHERE
        po.Id IN
            (
                SELECT
                    pp.Id
                FROM #POPaging pp
            );
    --Lấy tổng thành tiền từng PO(nếu nhận hàng thiếu thì sẽ lấy tổng số lượng nhận * đơn giá + VAT ngược lại số lượng * đơn giá) 
    SELECT
        tmp.PurchaseOrderId, SUM(tmp.totalprice) AS totalprice
    INTO
        #POTotalPrice
    FROM
        (
            SELECT 
                   po.Id AS PurchaseOrderId, ROUND(   (CASE
                                                           WHEN pli.IsReceived = 1 THEN pod.Price * pli.QuantityReceived + pod.VatPrice
                                                           ELSE pod.Price * pli.Quantity + pod.VatPrice
                                                       END
                                                      ), 0
                                                  ) AS totalprice
            FROM PurchaseOrder po
            LEFT JOIN PODetail pod ON po.Id = pod.PurchaseOrderId
            LEFT JOIN POPRLineItem pli ON pod.Id = pli.PurchaseOrderDetailId
            WHERE
                po.Id IN
                    (
                        SELECT
                            pp.Id
                        FROM #POPaging pp
                    )
        ) AS tmp
    GROUP BY
        tmp.PurchaseOrderId;
    --Lấy thông tin PO được bù bởi PO nào
    SELECT
        apor.PurchaseOrderId, STRING_AGG(apor.AdditionalPurchaseOrderId, ',') AS AdditionalPOs
    INTO
        #AdditionPOs
    FROM dbo.AdditionalPurchaseOrderRef apor
    WHERE
        apor.PurchaseOrderId IN
            (
                SELECT
                    pp.Id
                FROM #POPaging pp
            )
    GROUP BY
        apor.PurchaseOrderId;
    --Lấy thông tin PO bù cho PO nào
    SELECT
        apor.AdditionalPurchaseOrderId, apor.PurchaseOrderId AS AdditionForPO
    INTO
        #AdditionForPOs
    FROM dbo.AdditionalPurchaseOrderRef apor
    WHERE
        apor.AdditionalPurchaseOrderId IN
            (
                SELECT
                    pp.Id
                FROM #POPaging pp
            );
    SELECT
        pif.CreateDate, pif.Id, pif.VendorName, STRING_AGG(pif.DepartmentName, ',') AS Departments, STRING_AGG(pif.ProjectName, ',') AS Projects, COALESCE(ptp.totalprice, 0) AS totalprice, STRING_AGG(CONCAT('{"StringId": "', pif.RequestName, '", "Id": ', COALESCE(pif.RequestId, 0), '}'), ';') AS FromRequest, pif.Status, pif.IsCompensationPO, apo.AdditionalPOs, COALESCE(afpo.AdditionForPO, 0) AS AdditionForPO, @totalRecord AS TotalRecord
    FROM #POInfo pif
    LEFT JOIN #AdditionPOs apo ON pif.Id = apo.PurchaseOrderId
    LEFT JOIN #AdditionForPOs afpo ON pif.Id = afpo.AdditionalPurchaseOrderId
    JOIN #POTotalPrice ptp ON pif.Id = ptp.PurchaseOrderId
    GROUP BY
        pif.CreateDate, pif.Id, pif.VendorName, pif.Status, ptp.totalprice, pif.IsCompensationPO, apo.AdditionalPOs, COALESCE(afpo.AdditionForPO, 0)
    ORDER BY
        pif.CreateDate DESC;
END