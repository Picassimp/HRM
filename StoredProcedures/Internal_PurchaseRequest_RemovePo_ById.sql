CREATE OR ALTER PROCEDURE Internal_PurchaseRequest_RemovePo_ById
(
	@id INT
)
AS
BEGIN
	-- Lấy danh sách PoLineItem sẽ xóa
    SELECT 
		poli.Id AS PoLineItemId,
		poli.PurchaseOrderDetailId AS PoDetailId
	INTO #PoLineItemTemp
	FROM dbo.POPRLineItem poli
	JOIN dbo.PurchaseRequestLineItem prli ON prli.Id = poli.PORequestLineItemId
	WHERE prli.PurchaseRequestId = @id

	-- Xóa PoLineItem
	DELETE FROM dbo.POPRLineItem 
	WHERE EXISTS (SELECT 1 FROM #PoLineItemTemp WHERE #PoLineItemTemp.PoLineItemId = POPRLineItem.Id)

	-- Lấy danh sách PoDetail để xóa những PoDetail có PoLineItem = 0
	SELECT 
		pd.Id, 
		pd.PurchaseOrderId,
		(
			SELECT COUNT(*) 
			FROM dbo.POPRLineItem poli
			WHERE poli.PurchaseOrderDetailId = pd.Id
		) AS PoLineItemCount
	INTO #PoDetailTemp
	FROM dbo.PODetail pd 
	WHERE EXISTS(SELECT 1 FROM #PoLineItemTemp WHERE pd.Id = #PoLineItemTemp.PoDetailId)

	-- Xóa những PoDetail không có PoLineItem nào
	DELETE dbo.PODetail
	WHERE EXISTS (SELECT 1 FROM #PoDetailTemp WHERE PODetail.Id = #PoDetailTemp.Id AND #PoDetailTemp.PoLineItemCount = 0) 

	-- Cập nhật PoDetail.Quantity sau khi xóa PoLineItem
	UPDATE dbo.PODetail
	SET Quantity = 
	(
		SELECT SUM(poli.Quantity)
		FROM dbo.POPRLineItem poli 
		WHERE poli.PurchaseOrderDetailId = PODetail.Id
	)
	WHERE EXISTS (SELECT 1 FROM #PoDetailTemp WHERE PODetail.Id = #PoDetailTemp.Id)

	-- Lấy danh sách Po để xóa Po.PoDetailCount = 0
	;WITH po_cte AS
	(
		SELECT 
			po.Id,
			(SELECT COUNT(*) 
			FROM dbo.PODetail pd
			WHERE pd.PurchaseOrderId = po.Id) AS PoDetailCount 
		FROM dbo.PurchaseOrder po 
		WHERE EXISTS(SELECT 1 FROM #poDetailTemp WHERE #poDetailTemp.PurchaseOrderId = po.Id)
	)

	-- Lấy ra po không có poDetail
	SELECT po_cte.Id AS PoId
	INTO #PoIds
	FROM po_cte WHERE po_cte.PoDetailCount = 0

	-- Xóa kế hoạch thanh toán
	DELETE FROM dbo.PaymentPlan
	WHERE EXISTS (SELECT 1 FROM #PoIds WHERE #PoIds.PoId = PaymentPlan.PurchaseOrderId)

	-- Xóa những Po không có PoDetail
	DELETE FROM dbo.PurchaseOrder
	WHERE EXISTS (SELECT 1 FROM #PoIds WHERE #PoIds.PoId = PurchaseOrder.Id)

	DROP TABLE #PoLineItemTemp
	DROP TABLE #poDetailTemp
	DROP TABLE #PoIds
END