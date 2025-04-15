CREATE OR ALTER PROCEDURE [dbo].[Internal_PurchaseRequest_GetEbInfoByLineItemIds]
(
	@ids VARCHAR(MAX)
)
AS
BEGIN
	SELECT value AS LineItemId
	INTO #LineItemIds
	FROM STRING_SPLIT(@ids, ',')

    SELECT DISTINCT
		ebli.PORequestLineItemId AS LineItemId,
		ebd.ExportBillId,
		ebli.Quantity AS ExportQuantity
	FROM dbo.ExportBillLineItem ebli 
	JOIN dbo.ExportBillDetail ebd ON ebd.Id = ebli.ExportBillDetailId
	WHERE EXISTS (SELECT 1 FROM #LineItemIds WHERE #LineItemIds.LineItemId = ebli.PORequestLineItemId)

	DROP TABLE #LineItemIds
END;