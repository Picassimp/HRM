CREATE OR ALTER PROCEDURE [dbo].[Internal_GetPurchaseRequestDropdown_PO](@purchaseOrderId INT,@isCompensationPO bit)
AS
BEGIN
    DECLARE @rejectStatus INT = 3;
    DECLARE @cancelStatus INT = 4;
    DECLARE @Pending INT = 0;
    DECLARE @ManagerRejected INT = 1;
	DECLARE @HrRejected INT = 2;
	DECLARE @AccountantRejected INT = 3;
	DECLARE @DirectorRejected INT = 4;
    DECLARE @ManagerUpdateRequest INT = 5;
    --Lấy các LineItem của PO
    SELECT
        pli.PORequestLineItemId
    INTO
        #POLineItem
    FROM dbo.PODetail pd
    JOIN dbo.POPRLineItem pli ON pd.Id = pli.PurchaseOrderDetailId
    WHERE
        pd.PurchaseOrderId = @purchaseOrderId;
    --Lấy những request và số lượng yc của rq đó
    SELECT
        prli.PurchaseRequestId, SUM(prli.Quantity) AS RequestQty
    INTO
        #RequestInfo
    FROM dbo.PurchaseRequest pr
    JOIN dbo.PurchaseRequestLineItem prli ON pr.Id = prli.PurchaseRequestId
                                             AND pr.ReviewStatus NOT IN (@Pending,@ManagerRejected,@HrRejected,@AccountantRejected,@DirectorRejected,@ManagerUpdateRequest)
    GROUP BY
        prli.PurchaseRequestId;
    --Lấy ra những rq đã chọn hết
    SELECT
        PurchaseRequestId
    INTO
        #FinishedRQ
    FROM
        (
            SELECT
                prli.PurchaseRequestId, prli.Id,
                --check 2 bảng cùng tồn tại value
                CASE
                    WHEN EXISTS
                             (
                                 SELECT
                                     1
                                 FROM #POLineItem pli2
                                 WHERE
                                     pli2.PORequestLineItemId = prli.Id
                             ) THEN 1
                    ELSE 0
                END AS isExists
            FROM dbo.PurchaseRequestLineItem prli
        ) t
    GROUP BY
        PurchaseRequestId
    HAVING
        MIN(isExists) = 1;
    --Lấy các rq đã tạo ở PO
    SELECT
        prli.PurchaseRequestId, SUM(prli.Quantity) AS RequestQty, SUM(COALESCE(   CASE
                                                                                      WHEN pli.IsReceived = 0 THEN pli.Quantity
                                                                                      ELSE pli.QuantityReceived
                                                                                  END, 0
                                                                              )
                                                                     ) AS POQuantity
    INTO
        #RequestPOInfo
    FROM dbo.POPRLineItem pli
    LEFT JOIN dbo.PurchaseRequestLineItem prli ON pli.PORequestLineItemId = prli.Id
    GROUP BY
        prli.PurchaseRequestId;
    --Lấy các rq đã tạo phiếu xuất
    SELECT
        prli.PurchaseRequestId, SUM(prli.Quantity) AS RequestQty, COALESCE(SUM(ebli.Quantity), 0) AS EBQuantity
    INTO
        #RequestEBInfo
    FROM dbo.ExportBill eb
    JOIN dbo.ExportBillDetail ebd ON eb.Id = ebd.ExportBillId
    JOIN dbo.ExportBillLineItem ebli ON ebd.Id = ebli.ExportBillDetailId
    JOIN dbo.PurchaseRequestLineItem prli ON ebli.PORequestLineItemId = prli.Id
    WHERE
        eb.Status <> @rejectStatus
        AND eb.Status <> @cancelStatus
    GROUP BY
        prli.PurchaseRequestId;
    SELECT
        ri.PurchaseRequestId AS Id, pr.Name
	INTO #RQDropdown
    FROM #RequestInfo ri
	JOIN dbo.PurchaseRequest pr ON ri.PurchaseRequestId = pr.Id
    LEFT JOIN #RequestPOInfo rpi ON ri.PurchaseRequestId = rpi.PurchaseRequestId
    LEFT JOIN #RequestEBInfo rei ON ri.PurchaseRequestId = rei.PurchaseRequestId
    WHERE
        ri.PurchaseRequestId NOT IN
            (
                SELECT
                    ufr.PurchaseRequestId
                FROM #FinishedRQ ufr
            )
        AND ri.RequestQty <> COALESCE(rpi.POQuantity, 0) + COALESCE(rei.EBQuantity, 0);
	IF @isCompensationPO = 1
	BEGIN
		DECLARE @ogPO INT = (SELECT apor.PurchaseOrderId FROM dbo.AdditionalPurchaseOrderRef apor WHERE apor.AdditionalPurchaseOrderId = @purchaseOrderId)
		SELECT DISTINCT rd.*
		FROM dbo.PODetail pd
		JOIN dbo.POPRLineItem pli ON pd.Id = pli.PurchaseOrderDetailId
		JOIN dbo.PurchaseRequestLineItem prli ON pli.PORequestLineItemId = prli.Id
		JOIN #RQDropdown rd ON prli.PurchaseRequestId = rd.Id
		WHERE pd.PurchaseOrderId = @ogPO
	END
	ELSE
	BEGIN
	    SELECT * FROM
		#RQDropdown rd
	END
END;