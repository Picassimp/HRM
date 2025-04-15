CREATE OR ALTER PROCEDURE [dbo].[Internal_GetPaymentPlanPaging](@countSkip INT, @pageSize INT, @keyword NVARCHAR(MAX), @purchaseOrderId VARCHAR(50), @status BIT)
AS
BEGIN
    SELECT
        ROW_NUMBER() OVER (ORDER BY
                               tmp.PurchaseRequestId DESC
                          ) AS Rowcounts, tmp.*
    INTO
        #Payment
    FROM
        (
            SELECT DISTINCT
                   pp.Id AS PaymentId, pr.Name AS RequestName, pr.Id AS PurchaseRequestId, d.Name AS DepartmentName, po.Id AS PurchaseOrderId, pp.PayDate, pp.PaymentAmount, pp.PaymentStatus, pr.ReviewStatus
            FROM dbo.PurchaseOrder po
            JOIN dbo.PODetail pd ON po.Id = pd.PurchaseOrderId
            JOIN dbo.POPRLineItem pli ON pli.PurchaseOrderDetailId = pd.Id
            JOIN dbo.PaymentPlan pp ON po.Id = pp.PurchaseOrderId
            JOIN dbo.PurchaseRequestLineItem prli ON pli.PORequestLineItemId = prli.Id
            JOIN dbo.PurchaseRequest pr ON pr.Id = prli.PurchaseRequestId
            JOIN dbo.UserInternal ui ON ui.Id = pr.UserId
            JOIN dbo.Department d ON ui.DepartmentId = d.Id
            WHERE
                (
                    COALESCE(@keyword, '') = ''
                    OR pr.Name LIKE N'%' + @keyword + '%'
                )
                AND
                    (
                        ISNULL(@purchaseOrderId, '') = ''
                        OR po.Id = CAST(@purchaseOrderId AS INT)
                    )
                AND
                    (
                        ISNULL(CAST(@status AS VARCHAR(1)),'') = ''
                        OR pp.PaymentStatus = @status
                    )
        ) AS tmp;
    --Lấy các đợt thanh toán
    SELECT
        ROW_NUMBER() OVER (PARTITION BY
                               pp.PurchaseOrderId
                           ORDER BY
                               pp.CreateDate
                          ) AS Batch, pp.Id, pp.PurchaseOrderId
    INTO
        #PaymentBatch
    FROM dbo.PaymentPlan pp;
    DECLARE @totalRecord BIGINT =
                (
                    SELECT
                        MAX(p.Rowcounts)
                    FROM #Payment p
                );
    SELECT
        p.*, pb.Batch, @totalRecord AS TotalRecord
    FROM #Payment p
    JOIN #PaymentBatch pb ON p.PaymentId = pb.Id
    WHERE
        p.Rowcounts > (@countSkip * @pageSize)
        AND p.Rowcounts <= ((@countSkip + 1) * @pageSize)
    ORDER BY
        p.PurchaseRequestId DESC;
END;
