CREATE OR ALTER PROCEDURE [dbo].[Internal_PurchaseRequest_GetEbDtoByPrIds]
(
	@prIds VARCHAR(MAX)
)
AS
BEGIN
	SELECT value AS prId 
	INTO #prIds
	FROM STRING_SPLIT(@prIds, ',')

    SELECT prli.PurchaseRequestId,
		ebd.ExportBillId,
		ebli.Quantity AS ExportQuantity
	FROM dbo.PurchaseRequestLineItem prli
	JOIN dbo.ExportBillLineItem ebli ON ebli.PORequestLineItemId = prli.Id
	JOIN dbo.ExportBillDetail ebd ON ebd.Id = ebli.ExportBillDetailId
	WHERE EXISTS (SELECT 1 FROM #prIds WHERE #prIds.prId = prli.PurchaseRequestId)

	DROP TABLE #prIds
END;