using InternalPortal.ApplicationCore.Models;
using InternalPortal.ApplicationCore.Models.CriteriaModel;
using InternalPortal.ApplicationCore.Models.PagingModel;
using InternalPortal.ApplicationCore.Models.User;
using InternalPortal.ApplicationCore.Models.WorkFromHomeApplicationModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InternalPortal.ApplicationCore.Interfaces.Business
{
    public interface IWorkFromHomeApplicationService
    {
        Task<PagingResponseModel<WorkFromHomeApplicationPagingModel>> GetAllWithPagingAsync(WorkFromHomeApplicationCriteriaModel model, int userId);
        Task<CombineResponseModel<WorkFromHomeApplicationNotificationModel>> PrepareCreateAsync(WorkFromHomeApllicationRequest model, UserDtoModel user);
        Task<CombineResponseModel<WorkFromHomeApplicationNotificationModel>> PrepareUpdateAsync(int applicationId, WorkFromHomeApllicationRequest model, UserDtoModel user);
        Task SendMailAsync(int applicationId);
        Task SendNotificationAsync(WorkFromHomeApplicationNotificationModel notificationRequest);
        Task<List<WorkFromHomeApplicationPagingMobileModel>> GetAllWithPagingMobileAsync(WorkFromHomeApplicationSearchMobileModel searchModel, int userId);
    }
}
