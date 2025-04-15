CREATE OR ALTER PROCEDURE [dbo].[Internal_GetDetailForEBDetail](@exportBillId INT, @exportBillDetailId INT)
AS
BEGIN
    DECLARE @lackReceivedStatus INT = 5;
    DECLARE @fullReceivedStatus INT = 4;
    DECLARE @rejectStatus INT = 3;
    DECLARE @cancelStatus INT = 4;
	DECLARE @purchaseOrderRejectStatus INT = 6;
    --Lấy số lượng yêu cầu cho từng rq
    SELECT
        ebli.Id, ebli.PORequestLineItemId, pr.Id AS RequestId, pr.Name, prli.Quantity AS RequestQty, ebli.Quantity, pr.ReviewStatus
    INTO
        #Total
    FROM dbo.ExportBillDetail ebd
    JOIN dbo.ExportBillLineItem ebli ON ebli.ExportBillDetailId = ebd.Id
    JOIN dbo.PurchaseRequestLineItem prli ON ebli.PORequestLineItemId = prli.Id
    JOIN dbo.PurchaseRequest pr ON pr.Id = prli.PurchaseRequestId
    WHERE
        ebd.Id = @exportBillDetailId;
    SELECT
        t.PORequestLineItemId AS POLineItemId
    INTO
        #LineItemsFilter
    FROM #Total t;
    --Lấy các PO đã tạo
    SELECT
        tmp.PORequestLineItemId, STRING_AGG(tmp.FromPOs, ',') AS FromPOs, SUM(tmp.FromPOQty) AS FromPOQty
    INTO
        #FromPOs
    FROM
        (
            SELECT
                pli.PORequestLineItemId, STRING_AGG(po.Id, ',') AS FromPOs,SUM(CASE
                                                                                WHEN pli.IsReceived = 1 THEN (pli.QuantityReceived)
                                                                                ELSE (pli.Quantity)
                                                                            END) AS FromPOQty
            FROM dbo.PurchaseOrder po
            JOIN dbo.PODetail pd ON po.Id = pd.PurchaseOrderId
            JOIN dbo.POPRLineItem pli ON pli.PurchaseOrderDetailId = pd.Id
            WHERE
				po.Status <> @purchaseOrderRejectStatus
				AND
                pli.PORequestLineItemId IN
                    (
                        SELECT
                            lif.POLineItemId
                        FROM #LineItemsFilter lif
                    )
            GROUP BY
                pli.PORequestLineItemId
        ) AS tmp
    GROUP BY(tmp.PORequestLineItemId);
    --Lấy các phiếu xuất khác
    SELECT
        ebli.PORequestLineItemId, SUM(ebli.Quantity) AS ExportQty, STRING_AGG(eb.Id, ',') AS FromOtherEBs
    INTO
        #OtherEB
    FROM dbo.ExportBill eb
    JOIN dbo.ExportBillDetail ebd ON eb.Id = ebd.ExportBillId
    JOIN dbo.ExportBillLineItem ebli ON ebli.ExportBillDetailId = ebd.Id
    WHERE
        (
            eb.Status <> @rejectStatus
            AND eb.Status <> @cancelStatus
        )
        AND eb.Id <> @exportBillId
        AND ebli.PORequestLineItemId IN
                (
                    SELECT
                        lif.POLineItemId
                    FROM #LineItemsFilter lif
                )
    GROUP BY
        ebli.PORequestLineItemId;
    SELECT
        t.Id, t.RequestId, t.Name, t.RequestQty, fpo.FromPOs, oe.FromOtherEBs, 
		COALESCE(fpo.FromPOQty, 0) AS FromPOQty, COALESCE(oe.ExportQty, 0) AS ExportQty, t.Quantity,t.ReviewStatus
    FROM #Total t
    LEFT JOIN #FromPOs fpo ON t.PORequestLineItemId = fpo.PORequestLineItemId
    LEFT JOIN #OtherEB oe ON t.PORequestLineItemId = oe.PORequestLineItemId;
END;