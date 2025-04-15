CREATE OR ALTER PROCEDURE [dbo].[Internal_GetPOTotalPrice](@purchaseOrderId INT)
AS
BEGIN
    SELECT
        tmp.PurchaseOrderId, ROUND(SUM(tmp.TotalPriceWithVat), 0) AS TotalPriceWithVat, ROUND(SUM(tmp.TotalPriceWithoutVat), 0) AS TotalPriceWithoutVat
    FROM
        (
            SELECT
                pd.PurchaseOrderId, (CASE
                                         WHEN pli.IsReceived = 1 THEN pd.Price * pli.QuantityReceived + pd.VatPrice
                                         ELSE pd.Price * pli.Quantity + pd.VatPrice
                                     END
                                    ) AS TotalPriceWithVat, (CASE
                                                                 WHEN pli.IsReceived = 1 THEN pd.Price * pli.QuantityReceived
                                                                 ELSE pd.Price * pli.Quantity
                                                             END
                                                            ) AS TotalPriceWithoutVat
            FROM dbo.PODetail pd
            JOIN dbo.POPRLineItem pli ON pd.Id = pli.PurchaseOrderDetailId
            WHERE
                pd.PurchaseOrderId = @purchaseOrderId
        ) AS tmp
    GROUP BY
        tmp.PurchaseOrderId;
END;
