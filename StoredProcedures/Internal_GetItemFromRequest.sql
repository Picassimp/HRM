CREATE OR ALTER PROCEDURE [dbo].[Internal_GetItemFromRequest](@purchaseRequestId INT, @purchaseOrderId INT = 0)
AS
BEGIN
    DECLARE @lackReceivedStatus INT = 5;
    DECLARE @fullReceivedStatus INT = 4;
    DECLARE @rejectStatus INT = 3;
    DECLARE @cancelStatus INT = 4;
    DECLARE @purchaseOrderRejectStatus INT = 6;
    SELECT
        prli.Id, pc.Name AS CategoryName, p.Name,STRING_AGG(p2.Name,',') AS Detail, prli.Quantity AS RequestQty, p.Description
    INTO
        #LineItemInfo
    FROM dbo.PurchaseRequestLineItem prli
    JOIN dbo.Product p ON prli.ProductId = p.Id
    JOIN dbo.ProductCategory pc ON pc.Id = p.ProductCategoryId
    LEFT JOIN dbo.ProductKit pk ON pk.ProductId = p.Id
	LEFT JOIN dbo.Product p2 ON pk.SubProductId = p2.Id
    WHERE
        prli.PurchaseRequestId = @purchaseRequestId
	GROUP BY prli.Id,pc.Name,p.Name,prli.Quantity,p.Description;
    SELECT
        lii.Id
    INTO
        #LineItemFilter
    FROM #LineItemInfo lii;
    --Lấy thông tin từ PO khác
    SELECT
        pli.PORequestLineItemId, SUM(   CASE
                                            WHEN pli.IsReceived = 1 THEN pli.QuantityReceived
                                            ELSE pli.Quantity
                                        END
                                    ) AS QtyFromPO, STRING_AGG(pd.PurchaseOrderId, ',') AS FromPOs
    INTO
        #FromPOs
    FROM dbo.POPRLineItem pli
    JOIN dbo.PODetail pd ON pli.PurchaseOrderDetailId = pd.Id
    JOIN dbo.PurchaseOrder po ON pd.PurchaseOrderId = po.Id
    WHERE
        pd.PurchaseOrderId <> @purchaseOrderId
        AND po.Status <> @purchaseOrderRejectStatus
        AND pli.PORequestLineItemId IN
                (
                    SELECT
                        lif.Id
                    FROM #LineItemFilter lif
                )
    GROUP BY(pli.PORequestLineItemId);
    --Lấy thông tin từ phiếu xuất
    SELECT
        ebli.PORequestLineItemId, SUM(ebli.Quantity) AS QtyFromEB, STRING_AGG(ebd.ExportBillId, ',') AS FromEBs
    INTO
        #FromEBs
    FROM dbo.ExportBillLineItem ebli
    JOIN dbo.ExportBillDetail ebd ON ebli.ExportBillDetailId = ebd.Id
    JOIN dbo.ExportBill eb ON ebd.ExportBillId = eb.Id
    WHERE
        (
            eb.Status <> @rejectStatus
            AND eb.Status <> @cancelStatus
        )
        AND ebli.PORequestLineItemId IN
                (
                    SELECT
                        lif.Id
                    FROM #LineItemFilter lif
                )
    GROUP BY(ebli.PORequestLineItemId);
    --Lấy thông tin PO hiện tại
    SELECT
        pli.PORequestLineItemId, pd.PurchaseOrderId
    INTO
        #CurrentPO
    FROM dbo.PODetail pd
    JOIN dbo.POPRLineItem pli ON pd.Id = pli.PurchaseOrderDetailId
    WHERE
        pd.PurchaseOrderId = @purchaseOrderId
        AND pli.PORequestLineItemId IN
                (
                    SELECT
                        lif.Id
                    FROM #LineItemFilter lif
                );
    SELECT
        lii.Id, lii.CategoryName, lii.Name, lii.Detail, lii.RequestQty, lii.Description, fpo.FromPOs, feb.FromEBs, 
		COALESCE(fpo.QtyFromPO, 0) AS QtyFromPO, COALESCE(feb.QtyFromEB, 0) AS QtyFromEB, 
		COALESCE(cp.PurchaseOrderId, 0) AS PurchaseOrderId
    FROM #LineItemInfo lii
    LEFT JOIN #FromPOs fpo ON lii.Id = fpo.PORequestLineItemId
    LEFT JOIN #FromEBs feb ON feb.PORequestLineItemId = lii.Id
    LEFT JOIN #CurrentPO cp ON cp.PORequestLineItemId = lii.Id;
END;