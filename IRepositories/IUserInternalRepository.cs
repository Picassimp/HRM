using InternalPortal.ApplicationCore.Entities;
using InternalPortal.ApplicationCore.Models.ProjectManagement;
using InternalPortal.ApplicationCore.Models.User;

namespace InternalPortal.ApplicationCore.Interfaces.Repositories
{
    public interface IUserInternalRepository : IRepository<UserInternal>
    {
        Task<UserInternal?> GetByObjectIdAsync(string objectId);
        Task<List<InternalUserDetailModel>> GetAllManagerUsersAsync();
        Task<UserDtoModel?> GetUserDtoByObjectIdAsync(string objectId);
        Task<List<InternalUserDetailModel>> GetUsersSubmitOverTimeApplicationManagerAsync(int managerId);
        Task<List<InternalUserDetailModel>> GetUsersSubmitOnSiteApplicationManagerAsync(int managerId);
        Task<List<InternalUserDetailModel>> GetUsersSubmitWorkFromHomeApplicationManagerAsync(int managerId);
        Task<List<ProjectDataModel>> GetProjectMemberFilterDataAsync();
        Task<List<UserInternal>> GetListUserAsync();
        Task<List<InternalUserDetailModel>> GetAllManagers();
        Task<List<InternalUserDetailModel>> GetUsersAsync();
        Task<List<InternalUserDetailModel>> GetUsersInLeaveApplicationByManagerIdAsync(int managerId);
        Task<UserInternal?> GetByEmailAsync(string email);
        Task<List<UserInternalDayOffResponse>> GetAllWithPagingAsync(int userId, UserDayOffCriteriaModel requestModel);
    }
}
