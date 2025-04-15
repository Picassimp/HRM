CREATE OR ALTER PROCEDURE [dbo].[Internal_Project_UserRelation_GetAll]
AS
BEGIN
	SELECT ur.LeadUserId, ur.MemberUserId, ui.FullName AS MemberUserName
	FROM dbo.UserRelation ur
	JOIN dbo.UserInternal ui ON ui.Id = ur.MemberUserId
	-- Chỉ lấy nhân viên đang làm việc và thuộc về 1 phòng ban
	WHERE ui.HasLeft = 0 AND ui.IsDeleted = 0 AND ui.DepartmentId IS NOT NULL 
END