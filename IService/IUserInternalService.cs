using InternalPortal.ApplicationCore.Entities;
using InternalPortal.ApplicationCore.Models;
using InternalPortal.ApplicationCore.Models.User;
using InternalPortal.ApplicationCore.Models.UserInternal;

namespace InternalPortal.ApplicationCore.Interfaces.Business
{
    public interface IUserInternalService
    {
        Task<InternalUserDetailModel?> GetDetailByObjectIdAsync(string objectId, string? managerObjectId = null);
        Task<List<InternalUserDetailModel>> GetAllManagerUsersAsync();
        Task<UserDtoModel?> GetUserDtoByObjectIdAsync(string objectId, bool isCache = true);
        Task UpdateDayOffAsync(UserInternal user, LeaveApplication leaveApplication);
        Task<List<InternalUserDetailModel>> GetUsersAsync();
        Task<CombineResponseModel<UserInternalForLeaveApplication>> GetDayOffByUserIdAsync(int userId);
        Task<CombineResponseModel<bool>> NormalUpdateAsync(int userId, UserInternalNormalRequest model);
        Task<UserInformationModel> GetProfileAsync(int userId);
        bool IsOfficialStaff(string? groupUserName);
        Task<PagingResponseModel<UserInternalDayOffResponse>> GetDayOffWithPagingAsync(int userId, UserDayOffCriteriaModel request);
    }
}
