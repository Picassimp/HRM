CREATE OR ALTER PROCEDURE [dbo].[Internal_GetAccountantPaymentRequestPaging](
@countSkip INT, 
@pageSize INT, 
@userId NVARCHAR(MAX), 
@keyword NVARCHAR(MAX), 
@status VARCHAR(50), 
@type NVARCHAR(1000), 
@reviewUserIds VARCHAR(50), 
@startDate DATETIME = NULL, 
@endDate DATETIME = NULL)
AS
BEGIN
    DECLARE @Pending INT = 0;
    DECLARE @ManagerReject INT = 1;
    SELECT
        @keyword = TRIM(@keyword);
    --ReviewUserFilter
    SELECT
        reviewUserTmp.ReviewUserId
    INTO
        #ReviewUserFilter
    FROM
        (
            SELECT
                TRIM(value) AS [ReviewUserId]
            FROM STRING_SPLIT(@reviewUserIds, ',')
        ) AS reviewUserTmp;
    --UserFilter
    SELECT
        userTmp.UserId
    INTO
        #UserFilter
    FROM
        (
            SELECT
                TRIM(value) AS [UserId]
            FROM STRING_SPLIT(@userId, ',')
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
    WHERE
        pr.ReviewStatus NOT IN (@Pending, @ManagerReject)
        AND
            (
                ISNULL(@userId, '') = ''
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
            )
        AND
            (
                ISNULL(@type, '') = ''
                OR pr.Type LIKE N'%' + @type + '%'
            )
        AND
            (
                (
                    ISNULL(@startDate, '') = ''
                    OR pr.CreateDate >= @startDate
                )
                AND
                    (
                        ISNULL(@endDate, '') = ''
                        OR pr.CreateDate <= @endDate
                    )
            )
        AND
            (
                ISNULL(@reviewUserIds, '') = ''
                OR EXISTS
        (
            SELECT
                1
            FROM #ReviewUserFilter ruf
            WHERE
                ruf.ReviewUserId = pr.ReviewUserId
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
        pr.Id, pr.Name, pr.CreateDate, ui.FullName AS FullName, ui2.FullName AS ReviewUserName, pr.ReviewStatus, pr.Type, prp.PaymentStatus, prp.PaymentAmount, prp.ProposePaymentDate, prp.Note, @totalRecord AS TotalRecord
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


