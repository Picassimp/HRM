using InternalPortal.ApplicationCore.Entities;
using InternalPortal.ApplicationCore.Models.PaDetail;

namespace InternalPortal.ApplicationCore.Interfaces.Repositories
{
    public interface IPaDetailRepository : IRepository<PaDetail>
    {
        Task<List<QuestionGroupMarkModel>> GetQuestionGroupMarksAsync(int paId, int paHistoryId);
        Task<List<PaFormQuestionsModel>> GetPaFormQuestionsAsync(int paId, int paHistoryId);
    }
}
