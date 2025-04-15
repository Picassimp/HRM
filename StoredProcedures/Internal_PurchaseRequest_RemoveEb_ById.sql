CREATE OR ALTER PROCEDURE Internal_PurchaseRequest_RemoveEb_ById
(
	@id INT
)
AS
BEGIN
	-- Lấy danh sách ExportBillLineItem sẽ xóa
    SELECT 
		ebli.Id AS EbLineItemId,
		ebli.ExportBillDetailId AS EbDetailId
	INTO #EbLineItemTemp
	FROM dbo.ExportBillLineItem ebli
	JOIN dbo.PurchaseRequestLineItem prli ON prli.Id = ebli.PORequestLineItemId
	WHERE prli.PurchaseRequestId = @id

	-- Xóa ExportBillLineItem
	DELETE FROM dbo.ExportBillLineItem 
	WHERE EXISTS (SELECT 1 FROM #EbLineItemTemp WHERE #EbLineItemTemp.EbLineItemId = ExportBillLineItem.Id)

	-- Lấy danh sách ExportBillDetail để xóa những record có EbLineItem = 0
	SELECT 
		ebd.Id, 
		ebd.ExportBillId,
		(
			SELECT COUNT(*) 
			FROM dbo.ExportBillLineItem ebli
			WHERE ebli.ExportBillDetailId = ebd.Id
		) AS EbLineItemCount
	INTO #EbDetailTemp
	FROM dbo.ExportBillDetail ebd 
	WHERE EXISTS(SELECT 1 FROM #EbLineItemTemp WHERE ebd.Id = #EbLineItemTemp.EbDetailId)

	-- Xóa những EbDetail không có EbLineItem nào
	DELETE dbo.ExportBillDetail
	WHERE EXISTS (SELECT 1 FROM #EbDetailTemp WHERE ExportBillDetail.Id = #EbDetailTemp.Id AND #EbDetailTemp.EbLineItemCount = 0) 

	-- Cập nhật ExportBillDetail.Quantity sau khi xóa EbLineItem
	UPDATE dbo.ExportBillDetail
	SET Quantity = 
	(
		SELECT SUM(ebli.Quantity)
		FROM dbo.ExportBillLineItem ebli
		WHERE ebli.ExportBillDetailId = ExportBillDetail.Id
	)
	WHERE EXISTS (SELECT 1 FROM #EbDetailTemp WHERE ExportBillDetail.Id = #EbDetailTemp.Id)

	-- Lấy danh sách ExportBill để xóa EbDetailCount = 0
	;WITH eb_cte AS
	(
		SELECT 
			eb.Id,
			(SELECT COUNT(*) 
			FROM dbo.ExportBillDetail ebd
			WHERE ebd.ExportBillId = eb.Id) AS EbDetailCount
		FROM dbo.ExportBill eb
		WHERE EXISTS(SELECT 1 FROM #EbDetailTemp WHERE #EbDetailTemp.ExportBillId = eb.Id)
	)

	-- Xóa những Po không có PoDetail
	DELETE FROM dbo.ExportBill
	WHERE EXISTS (SELECT 1 FROM eb_cte WHERE eb_cte.Id = ExportBill.Id AND eb_cte.EbDetailCount = 0)

	DROP TABLE #EbLineItemTemp
	DROP TABLE #EbDetailTemp
END