CREATE OR ALTER PROCEDURE [dbo].[Internal_WorkFromHomeApplication_GetTotalWFHDayInMonth]
(
	@userId INT, 
	@date DATETIME, 
	@beginMonth DATETIME, 
	@endMonth DATETIME
)
AS
BEGIN
    SELECT (ISNULL(
                (
                    SELECT TOP 1
                           dbo.CalcBussinessDays(@beginMonth, ToDate, 0)
                    FROM [dbo].[WorkFromHomeApplication]
                    WHERE
                        UserId = @userId
                        AND MONTH(FromDate) = MONTH(@date) - 1
                        AND MONTH(ToDate) = MONTH(@date)
						AND YEAR(FromDate) = YEAR(@date)
                        AND ReviewStatus <> 2
                ), 0
                  )
           ) + (ISNULL(
                    (
                        SELECT
                            SUM(dbo.CalcBussinessDays(FromDate, ToDate, IIF(PeriodType <> 0, 1, 0)))
                        FROM [dbo].[WorkFromHomeApplication]
                        WHERE
                            UserId = @userId
                            AND MONTH(FromDate) = MONTH(ToDate)
                            AND MONTH(ToDate) = MONTH(@date)
							AND YEAR(FromDate) = YEAR(@date)
                            AND ReviewStatus <> 2
                    ), 0
                      )
               ) + (ISNULL(
                        (
                            SELECT TOP 1
                                   dbo.CalcBussinessDays(FromDate, @endMonth, 0)
                            FROM [dbo].[WorkFromHomeApplication]
                            WHERE
                                UserId = @userId
                                AND MONTH(FromDate) = MONTH(@date)
                                AND MONTH(ToDate) = MONTH(@date) + 1
								AND YEAR(ToDate) = YEAR(@date)
                                AND ReviewStatus <> 2
                        ), 0
                          )
                   );
END;