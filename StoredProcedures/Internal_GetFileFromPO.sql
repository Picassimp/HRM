CREATE OR ALTER PROCEDURE [dbo].[Internal_GetFileFromPO](@purchaseOrderId INT)
AS
BEGIN
	--Lấy thông tin request
    SELECT DISTINCT
           pr.Name, pr.Id
	INTO #Request
    FROM dbo.POPRLineItem pli
    JOIN dbo.PODetail pd ON pli.PurchaseOrderDetailId = pd.Id
    JOIN dbo.PurchaseRequestLineItem prli ON pli.PORequestLineItemId = prli.Id
    JOIN dbo.PurchaseRequest pr ON pr.Id = prli.PurchaseRequestId
    WHERE
        pd.PurchaseOrderId = @purchaseOrderId;
	SELECT r.Id
	INTO #RequestFilter
	FROM #Request r
	SELECT pra.PurchaseRequestId,STRING_AGG(pra.FileUrl,',') AS FileUrls
	INTO #RequestFile
	FROM dbo.PurchaseRequestAttachment pra
	WHERE pra.PurchaseRequestId IN (SELECT rf.Id FROM #RequestFilter rf)
	GROUP BY(pra.PurchaseRequestId)
	SELECT r.Id,r.Name,rf.FileUrls
	FROM #Request r
	JOIN #RequestFile rf ON r.Id = rf.PurchaseRequestId
END;