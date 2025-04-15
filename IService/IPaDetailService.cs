using InternalPortal.ApplicationCore.Entities;
using InternalPortal.ApplicationCore.Models.Pa;
using InternalPortal.ApplicationCore.Models.PaDetail;

namespace InternalPortal.ApplicationCore.Interfaces.Business
{
    public interface IPaDetailService
    {
        Task<UserPaDetailResponse> GetUserPaDetailAsync(int paId, int evaluateeId);
        Task<UserPaFormDataResponse> GetUserPaFormAsync(int paId, int evaluateeId, int appraiserId);
        Task<List<QuestionGroupForm>> GetPaFormQuestionsAsync(int paId, int paHistoryId);
        Task<PaHistory> UpdatePaFormAsync(int appraiserId, PaFormDataRequest request);
        Task CompletePaFormAsync(int appraiserId, PaFormDataRequest request);
        Task<PaNoteResponse> GetOneToOneNoteAsync(int paId, int evaluateeId);
        Task UpdateOneToOneNoteAsync(PaNoteRequest request, int appraiserId);
    }
}
