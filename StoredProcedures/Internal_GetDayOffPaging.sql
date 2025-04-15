CREATE OR ALTER PROCEDURE [dbo].[Internal_GetDayOffPaging]
(
	@userId INT,
	@countSkip INT,
	@pageSize INT
)
AS
BEGIN
	;WITH paging AS (
		SELECT 
			ROW_NUMBER() OVER (ORDER BY ui.Name) as RowCounts,
			ui.Id,
			ui.FullName,
			ui.OffDay,
			ui.YearOffDay,
			ui.MaxDayOff,
			ui.OffDayUseRamainDayOffLastYear,
			ui.RemainDayOffLastYear,
			ui.GroupUserId,
			g.Name as GroupUserName,
			ui.SickDayOff,
			ui.OffDayForSick,
			ui.BonusDayOff,
			ui.MaxBorrowedDayOff,
			ui.BorrowedDayOff,
			ui.UsedBorrowedDayOff
		FROM dbo.UserInternal ui
		LEFT JOIN dbo.GroupUser g on ui.GroupUserId = g.Id
		WHERE ui.Id = @userId
	) SELECT max(p.RowCounts) TotalRecord, 
		0 [Id],
		NULL [FullName],
		NULL [OffDay],
		NULL [YearOffDay],
		NULL [MaxDayOff],
		NULL [OffDayUseRamainDayOffLastYear],
		NULL [RemainDayOffLastYear],
		NULL [GroupUserId],
		NULL [GroupUserName],
		NULL SickDayOff,
		NULL OffDayForSick,
		NULL BonusDayOff,
		NULL MaxBorrowedDayOff,
		NULL BorrowedDayOff,
		NULL UsedBorrowedDayOff
	FROM paging p
	UNION ALL
	SELECT * FROM paging p
	WHERE (p.RowCounts > (@countSkip * @pageSize) and p.RowCounts <= ((@countSkip + 1) * @pageSize))
END