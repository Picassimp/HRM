CREATE OR ALTER PROCEDURE [dbo].[Internal_PaDetail_GetPaFormQuestions]
(
    @paId int,
    @paHistoryId int
)
AS
BEGIN
    SELECT
    Question.QuestionGroupId AS QuestionGroupId,
    QuestionGroup.Name AS QuestionGroupName,
    QuestionGroup.MinPercentPerQuestionGroup AS QuestionGroupMinium,
    MarkPercentVersionDetail.Value AS QuestionGroupPercent,
    Question.Id AS QuestionId,
    Question.Content AS QuestionContent,
    Question.Description AS QuestionDescription,
    GuidelineRange.Score AS ScoreRange,
    GuidelineRange.Name AS ScoreRangeName
    from PaHistory
    JOIN PaScore ON PaScore.PaHistoryId = PaHistory.Id
    JOIN Question ON PaScore.QuestionId = Question.Id
    JOIN GuidelineRange ON GuidelineRange.QuestionId = Question.Id
    JOIN QuestionGroup ON Question.QuestionGroupId = QuestionGroup.Id
    JOIN MarkPercentVersionDetail ON PaHistory.LevelId = MarkPercentVersionDetail.LevelId and PaHistory.JobId = MarkPercentVersionDetail.JobId and QuestionGroup.Id = MarkPercentVersionDetail.QuestionGroupId
    JOIN MarkPercentVersion ON MarkPercentVersion.Id = MarkPercentVersionDetail.MarkPercentVersionId
    JOIN Pa ON MarkPercentVersion.Id = Pa.MarkPercentVersionId
    WHERE Pa.Id = @paId and PaHistory.Id = @paHistoryId
    ORDER BY QuestionGroup.SeqNumber, Question.SeqNumber
END