CREATE OR ALTER PROCEDURE [dbo].[Internal_PurchaseRequest_GetDtoById]
(
	@id INT
)
AS
BEGIN
    SELECT
		pr.Id,
		ui.FullName AS UserName,
		ui.Email,
		pr.CreatedDate,
		pr.Name AS PurchaseRequestName,
		ui.JobTitle,
		d.Name AS DepartmentName,
		reviewer.FullName AS ReviewUserName,
		pj.Name AS ProjectName,
		pr.IsUrgent,
		pr.EstimateDate,
		pr.Note,
		pr.ReviewStatus,
		pr.ManagerComment,
		pr.FirstHrComment,
		pr.SecondHrComment,
		pr.DirectorComment,
		pr.RejectReason,
		pra.FileUrl,
		p.ProductCategoryId,
		pc.Name AS ProductCategoryName,
		prli.ProductId,
		p.Name AS ProductName,
		p.Description AS ProductDescription,
		prli.Quantity AS LineItemQuantity,
		pk.SubProductId,
		sp.Name AS SubProductName,
		sp.Description AS SubProductDescription,
		pk.Quantity AS KitQuantity,
		pd.Price,
		po.VendorId,
		v.VendorName,
		po.Id AS PurchaseOrderId,
		poli.Quantity AS PurchaseQuantity,
		pof.FileUrl AS PoFileUrl,
		prli.Note AS LineItemNote,
		prli.ShoppingUrl AS LineItemShoppingUrl,
		pd.Vat,
		prli.Id AS LineItemId
	FROM dbo.PurchaseRequest pr
	JOIN dbo.UserInternal ui ON ui.Id = pr.UserId
	JOIN dbo.Department d ON d.Id = pr.DepartmentId
	LEFT JOIN dbo.PurchaseRequestAttachment pra ON pra.PurchaseRequestId = pr.Id
	JOIN dbo.PurchaseRequestLineItem prli ON prli.PurchaseRequestId = pr.Id
	JOIN dbo.Product p ON p.Id = prli.ProductId
	JOIN dbo.ProductCategory pc ON pc.Id = p.ProductCategoryId
	LEFT JOIN dbo.ProductKit pk ON pk.ProductId = p.Id
	LEFT JOIN dbo.Product sp ON sp.Id = pk.SubProductId
	LEFT JOIN dbo.POPRLineItem poli ON poli.PORequestLineItemId = prli.Id
	LEFT JOIN dbo.PODetail pd ON pd.Id = poli.PurchaseOrderDetailId
	LEFT JOIN dbo.PurchaseOrder po ON po.Id = pd.PurchaseOrderId
	LEFT JOIN dbo.Vendor v ON v.Id = po.VendorId
	LEFT JOIN dbo.PurchaseOrderFile pof ON pof.PurchaseOrderId = po.Id
	JOIN dbo.UserInternal reviewer ON reviewer.Id = pr.ReviewUserId
	LEFT JOIN dbo.Project pj ON pj.Id = pr.ProjectId
	WHERE pr.Id = @id
	GROUP BY
		pr.Id,
		ui.FullName,
		ui.Email,
		pr.CreatedDate,
		pr.Name,
		ui.JobTitle,
		d.Name,
		reviewer.FullName,
		pj.Name,
		pr.IsUrgent,
		pr.EstimateDate,
		pr.Note,
		pr.ReviewStatus,
		pr.ManagerComment,
		pr.FirstHrComment,
		pr.SecondHrComment,
		pr.DirectorComment,
		pr.RejectReason,
		pra.FileUrl,
		p.ProductCategoryId,
		pc.Name,
		prli.ProductId,
		p.Name,
		p.Description,
		prli.Quantity,
		pk.SubProductId,
		sp.Name,
		sp.Description,
		pk.Quantity,
		pd.Price,
		po.VendorId,
		v.VendorName,
		po.Id,
		poli.Quantity,
		pof.FileUrl,
		prli.Note,
		prli.ShoppingUrl,
		pd.Vat,
		prli.Id
END