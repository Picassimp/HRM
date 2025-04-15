CREATE OR ALTER PROCEDURE [dbo].[Internal_GetDetailForPODetail](@purchaseOrderDetailId INT, @purchaseOrderId INT)
AS
BEGIN
    DECLARE @lackReceivedStatus INT = 5;
    DECLARE @fullReceivedStatus INT = 4;
	DECLARE @rejectStatus INT = 3
	DECLARE @cancelStatus INT = 4
	DECLARE @purchaseOrderRejectStatus INT = 6;
    --Lấy số lượng yêu cầu cho từng rq
    SELECT
        pli.Id, pli.PORequestLineItemId, pr.Id AS RequestId, pr.Name, prli.Quantity AS RequestQty, pli.Quantity POQty, 
		COALESCE(pli.QuantityReceived, 0) AS QuantityReceived,pr.ReviewStatus,pli.IsReceived
    INTO
        #Total
    FROM dbo.PODetail pd
    JOIN dbo.POPRLineItem pli ON pd.Id = pli.PurchaseOrderDetailId
    JOIN dbo.PurchaseRequestLineItem prli ON prli.Id = pli.PORequestLineItemId
    JOIN dbo.PurchaseRequest pr ON prli.PurchaseRequestId = pr.Id
    WHERE
        pd.Id = @purchaseOrderDetailId;
    SELECT
        t.PORequestLineItemId AS POLineItemId
    INTO
        #LineItemsFilter
    FROM #Total t;
    --Lấy các PO đã tạo không phải PO đang xét
    SELECT
        pli.PORequestLineItemId,STRING_AGG(pd.PurchaseOrderId,',') AS PurchaseOrderId,
		SUM(CASE WHEN pli.IsReceived = 1 THEN pli.QuantityReceived ELSE pli.Quantity END) AS FromOtherPOQty
    INTO
        #FromPOs
    FROM dbo.PODetail pd
	JOIN dbo.PurchaseOrder po ON pd.PurchaseOrderId = po.Id
    JOIN dbo.POPRLineItem pli ON pli.PurchaseOrderDetailId = pd.Id
    WHERE
        pd.PurchaseOrderId <> @purchaseOrderId
		AND po.Status <> @purchaseOrderRejectStatus
        AND pli.PORequestLineItemId IN
                (
                    SELECT
                        lif.POLineItemId
                    FROM #LineItemsFilter lif
                )
	GROUP BY (pli.PORequestLineItemId)
    --Lấy thông tin các mặt hàng đã nhận ở PO khác
    SELECT
        pli.PORequestLineItemId,SUM(pli.QuantityReceived) AS TotalQtyReceived
	INTO
	#ReceivedItem
    FROM dbo.POPRLineItem pli
    JOIN dbo.PODetail pd ON pli.PurchaseOrderDetailId = pd.Id
    JOIN dbo.PurchaseOrder po ON pd.PurchaseOrderId = po.Id
    WHERE
        po.Id <> @purchaseOrderId
        AND
            (
                po.Status = @fullReceivedStatus
                OR po.Status = @lackReceivedStatus
            )
        AND pli.PORequestLineItemId IN
                (
                    SELECT
                        lif.POLineItemId
                    FROM #LineItemsFilter lif
                )
	GROUP BY(pli.PORequestLineItemId);
    --Lấy các Phiếu xuất 
    SELECT
        STRING_AGG(ebd.ExportBillId,',') AS ExportBillId, ebli.PORequestLineItemId,SUM(ebli.Quantity) AS ExportQty
    INTO
        #FromEBs
    FROM dbo.ExportBillDetail ebd
    JOIN dbo.ExportBillLineItem ebli ON ebli.ExportBillDetailId = ebd.Id
	JOIN dbo.ExportBill eb ON ebd.ExportBillId = eb.Id
    WHERE
		(eb.Status <> @rejectStatus AND eb.Status <> @cancelStatus)
		AND
        ebli.PORequestLineItemId IN
            (
                SELECT
                    lif.POLineItemId
                FROM #LineItemsFilter lif
            )
	GROUP BY (ebli.PORequestLineItemId)
    SELECT
        t.Id, t.RequestId, t.Name, t.RequestQty,COALESCE(fpo.PurchaseOrderId,NULL) AS PurchaseOrderId,
		COALESCE(feb.ExportBillId,NULL) AS ExportBillId,COALESCE(fpo.FromOtherPOQty,0) AS FromOtherPOQty,
		COALESCE(feb.ExportQty,0) AS ExportQty,t.POQty,t.QuantityReceived,ri.TotalQtyReceived,t.ReviewStatus,t.IsReceived
    FROM #Total t
    LEFT JOIN #FromPOs fpo ON fpo.PORequestLineItemId = t.PORequestLineItemId
    LEFT JOIN #FromEBs feb ON feb.PORequestLineItemId = t.PORequestLineItemId
	LEFT JOIN #ReceivedItem ri ON ri.PORequestLineItemId = t.PORequestLineItemId;
END;
