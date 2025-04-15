CREATE OR ALTER PROCEDURE [dbo].[Internal_PurchaseRequest_GetPoDtoByPrIds]
(
	@prIds VARCHAR(MAX)
)
AS
BEGIN
	SELECT value AS prId 
	INTO #prIds
	FROM STRING_SPLIT(@prIds, ',')

    SELECT 
		prli.PurchaseRequestId,
		pd.PurchaseOrderId,
		pd.Price,
		poli.Quantity AS PurchaseQuantity,
		po.VendorId,
		v.VendorName,
		pd.VAT,
		(pd.Quantity * pd.Price) AS TotalPricePerItem,
		CASE WHEN pd.VatPrice = 0 THEN (pd.Quantity * pd.Price) + (pd.Quantity * pd.Price * pd.Vat / 100)
		ELSE pd.Quantity * pd.Price + pd.VatPrice END AS TotalPricePerItemWithVat
	FROM dbo.PurchaseRequestLineItem prli
	JOIN dbo.POPRLineItem poli ON poli.PORequestLineItemId = prli.Id
	JOIN dbo.PODetail pd ON pd.Id = poli.PurchaseOrderDetailId
	JOIN dbo.PurchaseOrder po ON po.Id = pd.PurchaseOrderId
	LEFT JOIN dbo.Vendor v ON v.Id = po.VendorId
	WHERE EXISTS (SELECT 1 FROM #prIds WHERE #prIds.prId = prli.PurchaseRequestId)

	DROP TABLE #prIds
END;