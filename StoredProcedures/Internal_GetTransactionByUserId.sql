CREATE OR ALTER PROCEDURE [dbo].[Internal_GetTransactionByUserId](@userId INT, @month INT, @year INT)
AS
BEGIN
    SELECT
        i.Name, i.ImageUrl, it.Quantity, it.DiscountedPrice, it.TotalPrice, it.CreatedDate
    FROM Inventory i
    JOIN InventoryTransaction it ON i.Id = it.InventoryId
    WHERE
        it.UserId = @userId
        AND MONTH(it.CreatedDate) = @month
        AND YEAR(it.CreatedDate) = @year
    ORDER BY
        it.CreatedDate DESC;
END;