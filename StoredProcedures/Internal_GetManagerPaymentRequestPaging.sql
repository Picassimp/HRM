CREATE OR ALTER PROCEDURE [dbo].[Internal_GetManagerPaymentRequestPaging](
@countSkip INT, 
@pageSize INT, 
@userId INT,
@userIds VARCHAR(50), 
@keyword NVARCHAR(MAX), 
@status VARCHAR(50))
AS
BEGIN
    CREATE TABLE #PaymentRequest (Id INT,
                                  totaldetail INT,
                                  totalplan INT);
    INSERT INTO #PaymentRequest(Id, totaldetail, totalplan)
    SELECT
        prd.PaymentRequestId, SUM(CASE WHEN prd.VatPrice = 0 
		THEN ROUND(((prd.Quantity * prd.Price) + (prd.Quantity * prd.Price) * prd.Vat / 100),0) ELSE prd.Quantity * prd.Price + prd.VatPrice END), 0
    FROM dbo.PaymentRequestDetail prd
    GROUP BY
        prd.PaymentRequestId;
    INSERT INTO #PaymentRequest(Id, totaldetail, totalplan)
    SELECT
        prp.PaymentRequestId, 0, SUM(ROUND(prp.PaymentAmount,0))
    FROM dbo.PaymentRequestPlan prp
    GROUP BY
        prp.PaymentRequestId;
    SELECT
        Id
    INTO
        #TotalPaymentRequestFilter
    FROM #PaymentRequest
    GROUP BY
        Id
    HAVING
        ROUND(SUM(totaldetail),0) = ROUND(SUM(totalplan),0);
    DROP TABLE #PaymentRequest;
    SELECT
        @keyword = TRIM(@keyword);
    --UserFilter
    SELECT
        userTmp.UserId
    INTO
        #UserFilter
    FROM
        (
            SELECT
                TRIM(value) AS [UserId]
            FROM STRING_SPLIT(@userIds, ',')
        ) AS userTmp;
    --StatusFilter
    SELECT
        statusTmp.Status
    INTO
        #StatusFilter
    FROM
        (
            SELECT
                TRIM(value) AS [Status]
            FROM STRING_SPLIT(@status, ',')
        ) AS statusTmp;
    --Filter All
    SELECT
        ROW_NUMBER() OVER (ORDER BY
                               pr.CreateDate DESC
                          ) AS RowCounts, pr.Id
    INTO
        #PaymentRequestFilter
    FROM dbo.PaymentRequest pr
    JOIN #TotalPaymentRequestFilter tprf ON tprf.Id = pr.Id
    WHERE
        pr.ReviewUserId = @userId
        AND
            (
                ISNULL(@userIds, '') = ''
                OR EXISTS
        (
            SELECT
                1
            FROM #UserFilter uf
            WHERE
                uf.UserId = pr.CreateUserId
        )
            )
        AND
            (
                COALESCE(@keyword, '') = ''
                OR pr.Name LIKE N'%' + @keyword + '%'
            )
        AND
            (
                ISNULL(@status, '') = ''
                OR EXISTS
        (
            SELECT
                1
            FROM #StatusFilter sf
            WHERE
                sf.Status = pr.ReviewStatus
        )
            );
    --Paging
    DECLARE @totalRecord BIGINT =
                (
                    SELECT
                        MAX(prf.RowCounts) AS TotalRecord
                    FROM #PaymentRequestFilter prf
                );
    SELECT
        prf.Id
    INTO
        #PaymentRequestPaging
    FROM #PaymentRequestFilter prf
    WHERE
        prf.RowCounts > (@countSkip * @pageSize)
        AND prf.RowCounts <= ((@countSkip + 1) * @pageSize);
    SELECT
        pr.Id, pr.CreateDate, pr.Name, ui.FullName AS FullName, ui2.FullName AS ReviewUserName, pr.ReviewStatus, pr.Type,prp.PaymentAmount,prp.ProposePaymentDate,prp.Note, @totalRecord AS TotalRecord
    FROM dbo.PaymentRequest pr
    JOIN dbo.UserInternal ui ON pr.CreateUserId = ui.Id
    JOIN dbo.UserInternal ui2 ON pr.ReviewUserId = ui2.Id
	JOIN dbo.PaymentRequestPlan prp ON pr.Id = prp.PaymentRequestId
    WHERE
        pr.Id IN
            (
                SELECT
                    prp.Id
                FROM #PaymentRequestPaging prp
            )
    ORDER BY
        pr.CreateDate DESC;
END;