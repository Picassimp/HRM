CREATE OR ALTER PROCEDURE Internal_PurchaseOrder_UpdateStatus
(
	@ids VARCHAR(MAX)
)
AS 
BEGIN
	SELECT value AS PoId
	INTO #PoIds
	FROM STRING_SPLIT(@ids, ',')

	DECLARE @directorApproved INT = 12;

	DECLARE @waitingAccept INT = 1, @accept INT = 2;

	DECLARE @isDirectorApprovedAll BIT = 1;

	IF EXISTS
	(
		SELECT DISTINCT
			prli.PurchaseRequestId,
			pr.ReviewStatus
		FROM dbo.PurchaseOrder po 
		JOIN dbo.PODetail pd ON pd.PurchaseOrderId = po.Id
		JOIN dbo.POPRLineItem poli ON  poli.PurchaseOrderDetailId = pd.Id
		JOIN dbo.PurchaseRequestLineItem prli ON prli.Id = poli.PORequestLineItemId
		JOIN dbo.PurchaseRequest pr ON pr.Id = prli.PurchaseRequestId
		WHERE EXISTS(SELECT 1 FROM #PoIds WHERE #PoIds.PoId = po.Id) 
			AND pr.ReviewStatus NOT IN (@directorApproved)
	)
	BEGIN
		SET @isDirectorApprovedAll = 0
	END

	IF @isDirectorApprovedAll = 1 -- Director approved all request
		BEGIN
			UPDATE dbo.PurchaseOrder
			SET Status = @accept
			WHERE EXISTS(SELECT 1 FROM #PoIds WHERE #PoIds.PoId = PurchaseOrder.Id) 
		END
	ELSE 
		BEGIN
			UPDATE dbo.PurchaseOrder
			SET Status = @waitingAccept
			WHERE EXISTS(SELECT 1 FROM #PoIds WHERE #PoIds.PoId = PurchaseOrder.Id) 
		END

	DROP TABLE #PoIds
END