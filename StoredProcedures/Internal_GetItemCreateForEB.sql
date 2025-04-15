CREATE OR ALTER PROCEDURE [dbo].[Internal_GetItemCreateForEB](@purchaseRequestLineItemIds VARCHAR(MAX), @exportBillId INT = 0)
AS
BEGIN
    DECLARE @lackReceivedStatus INT = 5;
    DECLARE @fullReceivedStatus INT = 4;
    DECLARE @rejectStatus INT = 3;
    DECLARE @cancelStatus INT = 4;
    DECLARE @purchaseOrderRejectStatus INT = 6;
    --Lấy các LineItem
    SELECT
        LineItemTmp.LineItemId
    INTO
        #LineItemFilter
    FROM
        (
            SELECT
                TRIM(value) AS LineItemId
            FROM STRING_SPLIT(@purchaseRequestLineItemIds, ',')
        ) AS LineItemTmp;
    --Lấy các item đã tạo ở PO 
    SELECT
        pli.PORequestLineItemId, pd.ProductId, SUM(   CASE
                                                          WHEN pli.IsReceived = 1 THEN pli.QuantityReceived
                                                          ELSE pli.Quantity
                                                      END
                                                  ) AS TotalQtyFromPO
    INTO
        #CreatedItem
    FROM dbo.POPRLineItem pli
    JOIN dbo.PODetail pd ON pli.PurchaseOrderDetailId = pd.Id
    JOIN dbo.PurchaseOrder po ON pd.PurchaseOrderId = po.Id
    WHERE
        po.Status <> @purchaseOrderRejectStatus
        AND pli.PORequestLineItemId IN
                (
                    SELECT
                        lif.LineItemId
                    FROM #LineItemFilter lif
                )
    GROUP BY
        pli.PORequestLineItemId, pd.ProductId;
    --Lấy item từ phiếu xuất khác
    SELECT
        ebli.PORequestLineItemId, SUM(ebli.Quantity) AS TotalQtyFromEB
    INTO
        #ExportBillItem
    FROM dbo.ExportBillLineItem ebli
    JOIN dbo.ExportBillDetail ebd ON ebli.ExportBillDetailId = ebd.Id
    JOIN dbo.ExportBill eb ON ebd.ExportBillId = eb.Id
    WHERE
        (eb.Id <> @exportBillId)
        AND
            (
                eb.Status <> @rejectStatus
                AND eb.Status <> @cancelStatus
            )
        AND ebli.PORequestLineItemId IN
                (
                    SELECT
                        lif.LineItemId
                    FROM #LineItemFilter lif
                )
    GROUP BY(ebli.PORequestLineItemId);
    --Lấy các item đã được tạo ở po
    SELECT
        ci.PORequestLineItemId, ci.ProductId,ci.TotalQtyFromPO
    INTO
        #CreateItem
    FROM #CreatedItem ci;
    SELECT
        prli.Id, prli.ProductId,prli.Quantity - (COALESCE(ci.TotalQtyFromPO, 0) + COALESCE(ebi.TotalQtyFromEB, 0)) AS RemainQty
    FROM dbo.PurchaseRequestLineItem prli
    LEFT JOIN #CreateItem ci ON prli.Id = ci.PORequestLineItemId
    LEFT JOIN #ExportBillItem ebi ON prli.Id = ebi.PORequestLineItemId
    WHERE
        prli.Id IN
            (
                SELECT
                    lif.LineItemId
                FROM #LineItemFilter lif
            );
END;