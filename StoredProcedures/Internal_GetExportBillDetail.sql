CREATE OR ALTER PROCEDURE [dbo].[Internal_GetExportBillDetail](@exportBillId INT)
AS
BEGIN
    DECLARE @lackReceivedStatus INT = 5;
    DECLARE @fullReceivedStatus INT = 4;
    DECLARE @rejectStatus INT = 3;
    DECLARE @cancelStatus INT = 4;
    DECLARE @purchaseOrderRejectStatus INT = 6;
    --Lấy thông tin Product
    SELECT
        ebd.Id, pc.Name AS CategoryName, p.Name, STRING_AGG(p2.Name, ',') AS Detail, STRING_AGG(pk.Quantity, ',') AS KitQuantity, p.Description, ebd.Quantity
    INTO
        #ProductDetail
    FROM dbo.ExportBillDetail ebd
    JOIN dbo.Product p ON p.Id = ebd.ProductId
    JOIN dbo.ProductCategory pc ON pc.Id = p.ProductCategoryId
    LEFT JOIN dbo.ProductKit pk ON pk.ProductId = p.Id
    LEFT JOIN dbo.Product p2 ON pk.SubProductId = p2.Id
    WHERE
        ebd.ExportBillId = @exportBillId
    GROUP BY
        ebd.Id, pc.Name, p.Name, p.Description, ebd.Quantity;
    --Lấy số lượng yêu cầu
    SELECT
        ebd.Id, SUM(prli.Quantity) AS TotalRequestQty
    INTO
        #TotalRequest
    FROM dbo.ExportBillDetail ebd
    LEFT JOIN dbo.ExportBillLineItem ebli ON ebli.ExportBillDetailId = ebd.Id
    LEFT JOIN dbo.PurchaseRequestLineItem prli ON prli.Id = ebli.PORequestLineItemId
    WHERE
        ebd.ExportBillId = @exportBillId
    GROUP BY(ebd.Id);
    --Lấy các LineItem của phiếu xuất
    SELECT
        ebd.Id, ebli.PORequestLineItemId
    INTO
        #LineItemIdFilter
    FROM dbo.ExportBillDetail ebd
    LEFT JOIN dbo.ExportBillLineItem ebli ON ebd.Id = ebli.ExportBillDetailId
    WHERE
        ebd.ExportBillId = @exportBillId;
    --Lấy các item đã tạo ở PO
    SELECT
        SUM( CASE
            WHEN pli.IsReceived = 1 THEN pli.QuantityReceived
            ELSE pli.Quantity
        END) AS TotalQtyFromPO, pli.PORequestLineItemId, STRING_AGG(pd.PurchaseOrderId, ',') AS FromPOs
    INTO
        #POInfo
    FROM dbo.POPRLineItem pli
    LEFT JOIN dbo.PODetail pd ON pli.PurchaseOrderDetailId = pd.Id
    LEFT JOIN dbo.PurchaseOrder po ON pd.PurchaseOrderId = po.Id
    WHERE
        po.Status <> @purchaseOrderRejectStatus
        AND pli.PORequestLineItemId IN
                (
                    SELECT
                        liif.PORequestLineItemId
                    FROM #LineItemIdFilter liif
                    WHERE
                        liif.PORequestLineItemId IS NOT NULL
                )
    GROUP BY
        pli.PORequestLineItemId;
    --Lấy các item đã tạo ở phiếu xuất khác
    SELECT
        ebli.PORequestLineItemId, STRING_AGG(ebd.ExportBillId, ',') AS FromOtherEBs, SUM(ebli.Quantity) AS TotalQtyFromOtherEB
    INTO
        #OtherEBInfo
    FROM dbo.ExportBill eb
    LEFT JOIN dbo.ExportBillDetail ebd ON eb.Id = ebd.ExportBillId
    LEFT JOIN dbo.ExportBillLineItem ebli ON ebli.ExportBillDetailId = ebd.Id
    WHERE
        eb.Id <> @exportBillId
        AND
            (
                eb.Status <> @rejectStatus
                AND eb.Status <> @cancelStatus
            )
        AND ebli.PORequestLineItemId IN
                (
                    SELECT
                        liif.PORequestLineItemId
                    FROM #LineItemIdFilter liif
                    WHERE
                        liif.PORequestLineItemId IS NOT NULL
                )
    GROUP BY(ebli.PORequestLineItemId);
    --Lấy thông tin từ phiếu xuất hiện tại và phiếu xuất khác và PO
    SELECT
        liif.Id, SUM(oei.TotalQtyFromOtherEB) AS TotalQtyFromOtherEB, SUM(poi.TotalQtyFromPO) AS TotalQtyFromPO, STRING_AGG(poi.FromPOs, ',') AS FromPOs, STRING_AGG(oei.FromOtherEBs, ',') AS FromOtherEBs
    INTO
        #CombineEB
    FROM #LineItemIdFilter liif
    LEFT JOIN #OtherEBInfo oei ON liif.PORequestLineItemId = oei.PORequestLineItemId
    LEFT JOIN #POInfo poi ON liif.PORequestLineItemId = poi.PORequestLineItemId
    GROUP BY
        liif.Id;
    --Lấy các request cho phiếu xuất
    SELECT
        ebd.Id, STRING_AGG(p.Name, ',') AS Projects, STRING_AGG(d.Name, ',') AS Departments, STRING_AGG(CONCAT('{"StringId": "', pr.Name, '", "Id": ', pr.Id, '}'), ';') AS FromRequests,
		STRING_AGG(pr.ReviewStatus,',') AS ReviewStatuses
    INTO
        #EBRequest
    FROM dbo.ExportBillDetail ebd
    LEFT JOIN dbo.ExportBillLineItem ebli ON ebli.ExportBillDetailId = ebd.Id
    LEFT JOIN dbo.PurchaseRequestLineItem prli ON prli.Id = ebli.PORequestLineItemId
    LEFT JOIN dbo.PurchaseRequest pr ON prli.PurchaseRequestId = pr.Id
    LEFT JOIN dbo.Project p ON pr.ProjectId = p.Id
    LEFT JOIN dbo.UserInternal ui ON ui.Id = pr.UserId
    LEFT JOIN dbo.Department d ON d.Id = ui.DepartmentId
    WHERE
        ebd.ExportBillId = @exportBillId
    GROUP BY
        ebd.Id;
    SELECT
        pd.Id, er.FromRequests, pd.CategoryName, pd.Name, pd.Detail,pd.KitQuantity, tr.TotalRequestQty, 
		pd.Description, ce.FromPOs, ce.FromOtherEBs, COALESCE(ce.TotalQtyFromPO, 0) AS TotalQtyFromPO, 
		COALESCE(ce.TotalQtyFromOtherEB, 0) AS TotalQtyFromOtherEB, pd.Quantity, er.Departments, er.Projects,
		er.ReviewStatuses
    FROM #ProductDetail pd
    JOIN #EBRequest er ON pd.Id = er.Id
    JOIN #TotalRequest tr ON pd.Id = tr.Id
    JOIN #CombineEB ce ON pd.Id = ce.Id;
END;