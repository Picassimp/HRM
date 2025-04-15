using InternalPortal.ApplicationCore.Models;
using InternalPortal.ApplicationCore.Models.CriteriaModel;
using InternalPortal.ApplicationCore.Models.OverTimeApplicationModel;
using InternalPortal.ApplicationCore.Models.PagingModel;
using InternalPortal.ApplicationCore.Models.User;

namespace InternalPortal.ApplicationCore.Interfaces.Business
{
    public interface IOverTimeApplicationService
    {
        Task<PagingResponseModel<OverTimePagingModel>> GetAllWithPagingAsync(OverTimeCriteriaModel requestModel, int userId);
        Task<CombineResponseModel<OvertimeApplicationNotificationModel>> PrepareCreateAsync(OverTimeApplicationRequest request, UserDtoModel user);
        Task SendMailAsync(int applicationId);
        Task SendNotificationAsync(OvertimeApplicationNotificationModel overtimeApplication);
        Task<CombineResponseModel<OvertimeApplicationNotificationModel>> PrepareUpdateAsync(int applicationId, OverTimeApplicationRequest applicationRequest, int userId);
        Task<List<OverTimePagingMobileModel>> GetAllWithPagingForMobileAsync(OverTimeCriteriaModel requestModel, int userId);
    }
}
