CREATE OR ALTER PROCEDURE [dbo].[Internal_GetPurchaseOrderDetail](@purchaseOrderId INT)
AS
BEGIN
    DECLARE @lackReceivedStatus INT = 5;
    DECLARE @fullReceivedStatus INT = 4;
    DECLARE @rejectStatus INT = 3;
    DECLARE @cancelStatus INT = 4;
    DECLARE @purchaseOrderRejectStatus INT = 6;
    --Lấy thông tin của Product
    SELECT
        tmp.*, STRING_AGG(p2.Name, ',') AS Detail, STRING_AGG(pk.Quantity, ',') AS KitQuantity
    INTO
        #ProductDetail
    FROM
        (
            SELECT DISTINCT
                   pd.Id, pd.ProductId, pc.Name AS CategoryName, p.Name, p.Description, pd.ShoppingUrl, pd.Quantity, pd.Price, pd.Vat, pd.IsFromRequest,pd.VatPrice
            FROM dbo.PODetail pd
            JOIN dbo.POPRLineItem pli ON pd.Id = pli.PurchaseOrderDetailId
            JOIN dbo.Product p ON p.Id = pd.ProductId
            JOIN dbo.ProductCategory pc ON pc.Id = p.ProductCategoryId
            WHERE
                pd.PurchaseOrderId = @purchaseOrderId
        ) AS tmp
    LEFT JOIN dbo.ProductKit pk ON pk.ProductId = tmp.ProductId
    LEFT JOIN dbo.Product p2 ON pk.SubProductId = p2.Id
    GROUP BY
        tmp.Id, tmp.ProductId, tmp.CategoryName, tmp.Name, tmp.Description, tmp.ShoppingUrl, tmp.Quantity, tmp.Price, tmp.Vat, tmp.IsFromRequest,tmp.VatPrice;
    --Lấy thành tiền từng sản phẩm
    SELECT
        pd.Id, SUM(   CASE
                          WHEN pli.IsReceived = 1 THEN pd.Price * pli.QuantityReceived
                          ELSE pd.Price * pli.Quantity
                      END
                  ) AS TotalPrice
    INTO
        #ProductTotalPrice
    FROM dbo.PODetail pd
    JOIN dbo.POPRLineItem pli ON pd.Id = pli.PurchaseOrderDetailId
    WHERE
        pd.PurchaseOrderId = @purchaseOrderId
    GROUP BY
        pd.Id;
    --Lấy các LineItem của PO
    SELECT
        pd.Id, pli.PORequestLineItemId
    INTO
        #LineItemIdFilter
    FROM dbo.PODetail pd
    LEFT JOIN dbo.POPRLineItem pli ON pd.Id = pli.PurchaseOrderDetailId
    WHERE
        pd.PurchaseOrderId = @purchaseOrderId;
    --Lấy các thông tin đã tạo ở PO khác
    SELECT
        SUM(   CASE
                   WHEN pli.IsReceived = 1 THEN pli.QuantityReceived
                   ELSE pli.Quantity
               END
           ) AS TotalQtyFromOtherPO, pli.PORequestLineItemId, STRING_AGG(pd.PurchaseOrderId, ',') AS FromOtherPOs
    INTO
        #OtherPOInfo
    FROM dbo.POPRLineItem pli
    JOIN dbo.PODetail pd ON pli.PurchaseOrderDetailId = pd.Id
    JOIN dbo.PurchaseOrder po ON pd.PurchaseOrderId = po.Id
    WHERE
        pli.PORequestLineItemId IN
            (
                SELECT
                    liif.PORequestLineItemId
                FROM #LineItemIdFilter liif
                WHERE
                    liif.PORequestLineItemId IS NOT NULL
            )
        AND pd.PurchaseOrderId <> @purchaseOrderId
        AND po.Status <> @purchaseOrderRejectStatus
    GROUP BY
        pli.PORequestLineItemId;
    --Lấy thông tin các sản phẩm đã nhận ở PO hiện tại
    SELECT
        pd.Id, SUM(pli.QuantityReceived) AS TotalQtyTrueReceived
    INTO
        #TrueReceivedItem
    FROM dbo.POPRLineItem pli
    JOIN dbo.PODetail pd ON pli.PurchaseOrderDetailId = pd.Id
    JOIN dbo.PurchaseOrder po ON po.Id = pd.PurchaseOrderId
    WHERE
        pd.PurchaseOrderId = @purchaseOrderId
        AND pli.IsReceived = 1
        AND pli.PORequestLineItemId IN
                (
                    SELECT
                        liif.PORequestLineItemId
                    FROM #LineItemIdFilter liif
                    WHERE
                        liif.PORequestLineItemId IS NOT NULL
                )
    GROUP BY(pd.Id);
    --Lấy các sản phẩm đã nhận ở PO hiện tại(không quan tâm đã nhận thiếu hay không)
    SELECT
        pd.Id, SUM(pli.QuantityReceived) AS TotalQtyReceived
    INTO
        #ReceivedItem
    FROM dbo.POPRLineItem pli
    JOIN dbo.PODetail pd ON pli.PurchaseOrderDetailId = pd.Id
    JOIN dbo.PurchaseOrder po ON po.Id = pd.PurchaseOrderId
    WHERE
        pd.PurchaseOrderId = @purchaseOrderId
        AND pli.PORequestLineItemId IN
                (
                    SELECT
                        liif.PORequestLineItemId
                    FROM #LineItemIdFilter liif
                    WHERE
                        liif.PORequestLineItemId IS NOT NULL
                )
    GROUP BY(pd.Id);
    --Lấy thông tin của PO đã tạo với PO hiện tại
    SELECT
        liif.Id, STRING_AGG(opi.FromOtherPOs, ',') AS FromOtherPOs, SUM(opi.TotalQtyFromOtherPO) AS TotalQty
    INTO
        #CombinePO
    FROM #LineItemIdFilter liif
    LEFT JOIN #OtherPOInfo opi ON liif.PORequestLineItemId = opi.PORequestLineItemId
    GROUP BY(liif.Id);
    --Lấy thông tin từ phiếu xuất với các lineItem của PO
    SELECT
        liif.Id, ebli.PORequestLineItemId, STRING_AGG(ebd.ExportBillId, ',') AS FromEBs, SUM(ebli.Quantity) AS QtyFromEBs
    INTO
        #FromEBs
    FROM #LineItemIdFilter liif
    LEFT JOIN dbo.ExportBillLineItem ebli ON ebli.PORequestLineItemId = liif.PORequestLineItemId
    LEFT JOIN dbo.ExportBillDetail ebd ON ebd.Id = ebli.ExportBillDetailId
    LEFT JOIN dbo.ExportBill eb ON ebd.ExportBillId = eb.Id
    WHERE
        (
            eb.Status <> @rejectStatus
            AND eb.Status <> @cancelStatus
        )
        AND ebli.PORequestLineItemId IN
                (
                    SELECT
                        lif.PORequestLineItemId
                    FROM #LineItemIdFilter lif
                    WHERE
                        lif.PORequestLineItemId IS NOT NULL
                )
    GROUP BY
        liif.Id, ebli.PORequestLineItemId;
    --Lấy các request cho PO
    SELECT
        pd.Id, STRING_AGG(p.Name, ',') AS Projects, STRING_AGG(d.Name, ',') AS Departments, STRING_AGG(CONCAT('{"StringId": "', pr.Name, '", "Id": ', pr.Id, '}'), ';') AS FromRequests,
		STRING_AGG(pr.ReviewStatus,',')  AS ReviewStatuses
    INTO
        #PORequest
    FROM dbo.PODetail pd
    LEFT JOIN dbo.POPRLineItem pli ON pli.PurchaseOrderDetailId = pd.Id
    LEFT JOIN dbo.PurchaseRequestLineItem prli ON prli.Id = pli.PORequestLineItemId
    LEFT JOIN dbo.PurchaseRequest pr ON prli.PurchaseRequestId = pr.Id
    LEFT JOIN dbo.Project p ON pr.ProjectId = p.Id
    LEFT JOIN dbo.UserInternal ui ON ui.Id = pr.UserId
    LEFT JOIN dbo.Department d ON d.Id = ui.DepartmentId
    WHERE
        pd.PurchaseOrderId = @purchaseOrderId
    GROUP BY
        pd.Id;
    --Lấy số lượng yêu cầu
    SELECT
        pd.Id, SUM(prli.Quantity) AS TotalRequestQty
    INTO
        #TotalRequest
    FROM dbo.PODetail pd
    LEFT JOIN dbo.POPRLineItem pli ON pli.PurchaseOrderDetailId = pd.Id
    LEFT JOIN dbo.PurchaseRequestLineItem prli ON prli.Id = pli.PORequestLineItemId
    WHERE
        pd.PurchaseOrderId = @purchaseOrderId
    GROUP BY(pd.Id);
    SELECT
       DISTINCT pd.Id, pr.FromRequests, pd.CategoryName, pd.Name, pd.Detail, pd.KitQuantity, COALESCE(tr.TotalRequestQty, 0) AS TotalRequestQty, 
		COALESCE(tri.TotalQtyTrueReceived, 0) AS TotalQtyTrueReceived, COALESCE(ri.TotalQtyReceived, 0) AS TotalQtyReceived, 
		pd.Description, cp.FromOtherPOs, feb.FromEBs, COALESCE(feb.QtyFromEBs, 0) AS QtyFromEBs, COALESCE(cp.TotalQty, 0) AS QtyFromOtherPOs, 
		pd.Quantity, pd.Price, pr.Departments, pr.Projects, pd.ShoppingUrl, pd.Vat, pd.IsFromRequest, ptp.TotalPrice,pr.ReviewStatuses,pd.VatPrice
    FROM #ProductDetail pd
    JOIN #PORequest pr ON pd.Id = pr.Id
    JOIN #TotalRequest tr ON pd.Id = tr.Id
    JOIN #CombinePO cp ON pd.Id = cp.Id
    LEFT JOIN #FromEBs feb ON pd.Id = feb.Id
    LEFT JOIN #TrueReceivedItem tri ON pd.Id = tri.Id
    LEFT JOIN #ReceivedItem ri ON pd.Id = ri.Id
    JOIN #ProductTotalPrice ptp ON pd.Id = ptp.Id;
END;
