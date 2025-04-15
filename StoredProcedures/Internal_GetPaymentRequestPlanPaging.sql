CREATE OR ALTER PROCEDURE [dbo].[Internal_GetPaymentRequestPlanPaging](
@countSkip INT,
@pageSize INT,
@keyword NVARCHAR(MAX),
@status BIT, @isUrgent BIT = 0,
@startDate DATETIME,
@endDate DATETIME)
AS
BEGIN
    SELECT
        ROW_NUMBER() OVER (ORDER BY
                               tmp.PaymentRequestId DESC
                          ) AS Rowcounts, tmp.*
    INTO
        #Payment
    FROM
        (
            SELECT DISTINCT
                   prp.Id AS PaymentId,pr.CreateDate, pr.Name AS RequestName, pr.Id AS PaymentRequestId, prp.PaymentDate, prp.PaymentAmount, prp.PaymentStatus, prp.IsUrgent, pr.ReviewStatus,prp.ProposePaymentDate
            FROM dbo.PaymentRequest pr
            JOIN dbo.PaymentRequestPlan prp ON pr.Id = prp.PaymentRequestId
            WHERE
                (
                    COALESCE(@keyword, '') = ''
                    OR pr.Name LIKE N'%' + @keyword + '%'
                )
                AND
                    (
                        ISNULL(CAST(@status AS VARCHAR(1)),'') = ''
                        OR prp.PaymentStatus = @status
                    )
                AND
                    (
                        @isUrgent = 0
                        OR prp.IsUrgent = @isUrgent
                    )
				AND
				(
					ISNULL(@startDate,'') = ''
					OR CAST(prp.ProposePaymentDate AS DATE) >= @startDate
				)
				AND
				(
					ISNULL(@endDate,'') = ''
					OR CAST(prp.ProposePaymentDate AS DATE) <= @endDate
				)
        ) AS tmp;
    --Lấy các đợt thanh toán
    SELECT
        ROW_NUMBER() OVER (PARTITION BY
                               prp.PaymentRequestId
                           ORDER BY
                               prp.CreateDate
                          ) AS Batch, prp.Id, prp.PaymentRequestId
    INTO
        #PaymentBatch
    FROM dbo.PaymentRequestPlan prp;
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
        p.PaymentRequestId DESC;
END;
