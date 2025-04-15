CREATE OR ALTER PROCEDURE Internal_PurchaseRequest_GetDtoByIds
(
	@ids VARCHAR(MAX)
)
AS 
BEGIN
	SELECT 
		pr.Id,
		pr.Name,
		pr.ReviewStatus,
		SUM(prli.Quantity) AS TotalLineItemQuantity
	FROM dbo.PurchaseRequest pr 
	JOIN dbo.PurchaseRequestLineItem prli ON prli.PurchaseRequestId = pr.Id
	WHERE pr.Id IN (SELECT value FROM STRING_SPLIT(@ids, ','))
	GROUP BY 
		pr.Id,
		pr.ReviewStatus,
		pr.Name
END