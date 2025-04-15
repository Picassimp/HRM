using InternalPortal.ApplicationCore.Entities;
using InternalPortal.ApplicationCore.Models;
using InternalPortal.ApplicationCore.Models.LeaveApplication;
using InternalPortal.ApplicationCore.Models.User;

namespace InternalPortal.ApplicationCore.Interfaces.Business
{
    public interface ILeaveApplicationService
    {
        Task<CombineResponseModel<LeaveApplicationNotificationModel>> PrepareCreateAsync(LeaveApplicationRequest request, UserDtoModel register);
        Task<PagingResponseModel<LeaveApplicationPagingModel>> GetAllWithPagingAsync(LeaveApplicationPagingRequest request, int userId);
        Task SendEmailAsync(int leaveApplicationId);
        Task SendNotificationAsync(LeaveApplicationNotificationModel leaveApplication);
        Task<CombineResponseModel<LeaveApplicationNotificationModel>> PrepareUpdateAsync(int leaveApplicationId, LeaveApplicationRequest request, UserDtoModel register);
        Task<CombineResponseModel<LeaveApplication>> PrepareReviewAsync(int leaveApplicationId, ReviewModel request, UserDtoModel reviewer);
        Task<List<LeaveApplicationPagingMobileModel>> GetAllWithPagingMobileAsync(LeaveApplicationSearchMobileModel searchModel, int userId);
    }
}
