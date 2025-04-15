CREATE OR ALTER PROCEDURE [dbo].[Internal_PaDetail_GetQuestionGroupMarks]
(
    @paId int,
    @paHistoryId int
)
AS
    SET NOCOUNT ON
BEGIN
    select
	QuestionGroup.Id as Id,
	MarkPercentVersionDetail.Value as [Percent],
	Sum(PaScore.QuestionScore) as Mark,
	Count(case when PaScore.QuestionScore > 0 then PaScore.QuestionScore end) as [Count]
	from PaHistory
	join PaScore on PaScore.PaHistoryId = PaHistory.Id
	join Question on PaScore.QuestionId = Question.Id
	join QuestionGroup on Question.QuestionGroupId = QuestionGroup.Id
	join MarkPercentVersionDetail on PaHistory.LevelId = MarkPercentVersionDetail.LevelId and PaHistory.JobId = MarkPercentVersionDetail.JobId and QuestionGroup.Id = MarkPercentVersionDetail.QuestionGroupId
	join MarkPercentVersion on  MarkPercentVersion.Id = MarkPercentVersionDetail.MarkPercentVersionId
	join Pa on MarkPercentVersion.Id = Pa.MarkPercentVersionId
	where Pa.Id = @paId and PaHistory.Id = @paHistoryId                           
group by QuestionGroup.Id, MarkPercentVersionDetail.Value
END