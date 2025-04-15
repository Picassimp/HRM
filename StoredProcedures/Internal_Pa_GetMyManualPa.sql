CREATE OR ALTER PROCEDURE [dbo].[Internal_Pa_GetMyManualPa](
@userId INT
)
AS
BEGIN
select pa.Id, pa.Name, pa.Month, pa.Year, pa.IsPublic, pa.IsCompleted, pd.UserId, pr.AssessUserId, ui.FullName, ph.Status, ph.OneToOneNote
from Pa pa
    join PaDetail pd on pd.PaId = pa.Id
    join PaRelative pr on pr.PaDetailId = pd.Id
    join UserInternal ui on pd.UserId = ui.Id
    join PaHistory ph on ph.PaRelativeId = pr.Id
    where ((((pd.UserId = @userId and pr.AssessUserId Is Null) 
    or pr.AssessUserId = @userId)
    or pd.UserId = @userId and pr.AssessUserId Is Not Null)
        and pa.Type = 1 and pa.IsPublic = 'true')
END