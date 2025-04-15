CREATE OR ALTER PROCEDURE [dbo].[Internal_GetUserPaymentRequestPaging](@countSkip INT, @pageSize INT, @userId INT, @keyword NVARCHAR(MAX), @status VARCHAR(50))
AS
BEGIN
    SELECT
        @keyword = TRIM(@keyword);
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
    WHERE
        pr.CreateUserId = @userId
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
        pr.Id, pr.Name, pr.CreateDate, ui.FullName AS FullName, ui2.FullName AS ReviewUserName, pr.ReviewStatus, pr.Type,prp.PaymentStatus, @totalRecord AS TotalRecord
    FROM dbo.PaymentRequest pr
    JOIN dbo.UserInternal ui ON pr.CreateUserId = ui.Id
    JOIN dbo.UserInternal ui2 ON pr.ReviewUserId = ui2.Id
	LEFT JOIN dbo.PaymentRequestPlan prp ON pr.Id = prp.PaymentRequestId
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