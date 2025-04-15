CREATE OR ALTER PROCEDURE Internal_PurchaseRequest_GetSubProduct
(
	@ids VARCHAR(MAX)
)
AS
BEGIN
    SELECT 
		p.Id AS ProductId,
		sp.Id AS SubProductId,
		sp.Name AS SubProductName,
		sp.Description AS SubProductDescription,
		pk.Quantity AS KitQuantity
	FROM dbo.Product p
	JOIN dbo.ProductKit pk ON pk.ProductId = p.Id
	JOIN dbo.Product sp ON pk.SubProductId = sp.Id
	WHERE p.Id IN (SELECT value FROM STRING_SPLIT(@ids, ','))
END