CREATE OR ALTER PROCEDURE [dbo].[Internal_LeaveApplication_GetPaging]
(
	@countSkip INT, 
	@pageSize INT, 
	@userId INT, 
	@type VARCHAR(1), 
	@status VARCHAR(1), 
	@searchUserId INT, 
	@searchReviewerId INT, 
	@fromDate DATETIME, 
	@toDate DATETIME,
	@keysort VARCHAR(50),
	@orderByDescending BIT,
	@isNoPaging BIT
)
AS
BEGIN
    ;WITH paging AS
        (
            SELECT ROW_NUMBER() OVER (ORDER BY
					IIF(COALESCE(@keysort, '') = '', la.RegisterDate, NULL) DESC,
					IIF(@keysort = 'RegisterDate' and @orderByDescending = 0, la.RegisterDate, NULL) ASC,
					IIF(@keysort = 'RegisterDate' and @orderByDescending = 1, la.RegisterDate, NULL) DESC,
					IIF(@keysort = 'FromDate' and @orderByDescending = 0, la.FromDate, NULL) ASC,
					IIF(@keysort = 'FromDate' and @orderByDescending = 1, la.FromDate, NULL) DESC,
					IIF(@keysort = 'RegisterName' and @orderByDescending = 0, register.FullName, NULL) ASC,
					IIF(@keysort = 'RegisterName' and @orderByDescending = 1, register.FullName, NULL) DESC
				) AS RowCounts, 
				la.Id, 
				CAST(la.RegisterDate AS DATE) AS RegisterDate, 
				la.FromDate, 
				la.ToDate, 
				la.NumberDayOff, 
				la.LeaveApplicationNote, 
				la.ReviewStatus, 
				la.ReviewNote, 
				lat.[Name] AS LeaveApplicationType, 
				la.UserId, 
				ISNULL(register.FullName, register.[Name]) AS UserName, 
				la.ReviewUserId, 
				ISNULL(reviewer.FullName, reviewer.Name) AS ReviewUser,
				CAST(la.ReviewDate AS DATE) AS ReviewDate, 
				la.PeriodType, 
				register.JobTitle, 
				la.RelatedUserIds,
				la.IsAlertCustomer,
				la.HandoverUserId,
				handoverUser.FullName AS HandoverUserName,
				la.BorrowedDayOff,
				register.Avatar
            FROM dbo.LeaveApplication la
            JOIN dbo.LeaveApplicationType lat ON lat.Id = la.LeaveApplicationTypeId
            JOIN dbo.UserInternal reviewer ON reviewer.Id = la.ReviewUserId
            JOIN dbo.UserInternal register ON register.Id = la.UserId
			LEFT JOIN dbo.UserInternal handoverUser ON handoverUser.Id = la.HandoverUserId
			WHERE (COALESCE(@type, '') = '' OR (@type = 0 AND la.UserId = @userId)
											OR (@type = 1 AND la.ReviewUserId = @userId)
											OR (@type = 2 AND @userId IN (SELECT value FROM STRING_SPLIT(la.RelatedUserIds, ','))))
				AND (COALESCE(@status, '') = '' OR la.ReviewStatus = @status)
				AND (COALESCE(@searchUserId, '') = '' OR la.UserId = @searchUserId)
				AND (COALESCE(@searchReviewerId, '') = '' OR la.ReviewUserId = @searchReviewerId)
				AND ((COALESCE(@fromDate, '') = '' AND COALESCE(@toDate, '') = '') 
					OR NOT ((CAST(la.FromDate AS DATE) > CAST(@toDate AS DATE)) OR CAST(la.ToDate AS DATE) < CAST(@fromDate AS Date)))
        )
    SELECT MAX(p.RowCounts) TotalRecord, 
		0 Id, 
		NULL [RegisterDate], 
		GETUTCDATE() [FromDate], 
		GETUTCDATE() [ToDate], 
		0 [NumberDayOff],
		NULL [LeaveApplicationNote], 
		0 ReviewStatus, 
		NULL [ReviewNote], 
		NULL LeaveApplicationType, 
		0 [UserId], 
		NULL [UserName], 
		0 [ReviewUserId], 
		NULL [ReviewUser], 
		NULL [ReviewDate], 
		0 [PeriodType], 
		NULL JobTitle, 
		NULL [RelatedUserIds],
		0 IsAlertCustomer,
		0 HandoverUserId,
		NULL HandoverUserName,
		0 BorrowedDayOff,
		NULL Avatar
    FROM paging p
    UNION ALL
    SELECT *
    FROM paging p
    WHERE (@isNoPaging = 1 OR (p.RowCounts > (@countSkip * @pageSize) AND p.RowCounts <= (@countSkip + 1) * @pageSize));
END;