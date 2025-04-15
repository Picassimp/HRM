CREATE OR ALTER PROCEDURE Internal_PurchaseOrder_GetTotalPriceByPoIds(@ids VARCHAR(MAX))
AS
BEGIN
    SELECT
        po.Id AS PurchaseOrderId,ROUND( SUM(   (pd.Price * pd.Quantity) + CASE
                                                                        WHEN pd.VatPrice = 0 THEN (pd.Price * pd.Quantity * pd.Vat / 100)
                                                                        ELSE pd.VatPrice
                                                                    END
                                     ),0) AS TotalPrice
    FROM dbo.PurchaseOrder po
    JOIN dbo.PODetail pd ON pd.PurchaseOrderId = po.Id
    WHERE
        po.Id IN
            (
                SELECT
                    value
                FROM STRING_SPLIT(@ids, ',')
            )
    GROUP BY
        po.Id;
END;