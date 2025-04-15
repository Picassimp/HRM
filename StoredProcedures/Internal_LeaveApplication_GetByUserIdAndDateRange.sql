CREATE OR ALTER PROCEDURE [dbo].[Internal_LeaveApplication_GetByUserIdAndDateRange]
(
	@userId INT, 
	@fromDate DATETIME, 
	@toDate DATETIME
)
AS
BEGIN
    SELECT la.Id, la.ReviewStatus, la.PeriodType
    FROM dbo.LeaveApplication la
    WHERE la.UserId = @userId 
		AND NOT (CAST(la.FromDate AS DATE) > CAST(@toDate AS DATE) OR CAST(la.ToDate AS DATE) < CAST(@fromDate AS DATE))
END;