using InternalPortal.ApplicationCore.Models;
using InternalPortal.ApplicationCore.Models.CriteriaModel;
using InternalPortal.ApplicationCore.Models.OnsiteApplicationModel;
using InternalPortal.ApplicationCore.Models.PagingModel;
using InternalPortal.ApplicationCore.Models.User;

namespace InternalPortal.ApplicationCore.Interfaces.Business
{
    public interface IOnsiteApplicationService
    {
        Task<PagingResponseModel<OnsiteApplicationPagingModel>> GetAllWithPagingAsync(OnsiteApplicationCriteriaModel searchModel, int userId);
        Task<CombineResponseModel<OnsiteApplicationNotificationModel>> PrepareCreateAsync(OnsiteApplicationRequest applicationRequest, UserDtoModel user);
        Task SendMailAsync(int onsiteApplicationId);
        Task SendNotificationAsync(OnsiteApplicationNotificationModel onSiteApplication);
        Task<CombineResponseModel<OnsiteApplicationNotificationModel>> PrepareUpdateAsync(int id, OnsiteApplicationUpdateRequest applicationRequest, int userId);
        Task<List<OnsiteApplicationMobilePagingModel>> GetAllWithPagingMobileAsync(OnsiteApplicationMobileCriteriaModel searchModel, int userId);
    }
}
