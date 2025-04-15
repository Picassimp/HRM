using InternalPortal.ApplicationCore.Entities;
using InternalPortal.ApplicationCore.Models.ChromeExtension;
using InternalPortal.ApplicationCore.Models.ProjectManagement;

namespace InternalPortal.ApplicationCore.Interfaces.Repositories
{
    public interface IProjectTimesheetRepository : IRepository<ProjectTimeSheet>
    {
        Task<List<ProjectTimesheetPagingResponse>> UserGetProjectTimesheetPagingAsync(int userId, ProjectTimesheetUserPagingRequest model);
        Task<List<ProjectTimesheetSelfPagingResponse>> UserGetProjectTimesheetSelfPagingAsync(int userId, ProjectTimesheetSelfUserPagingRequest model);
        Task<List<ManageProjectTimesheetPagingResponse>> ManagerOrOwnerGetProjectPagingAsync(ProjectTimesheetPagingRequest model);
        Task<List<ProjectTimeSheet>> GetByProjectIdAndUserIdAsync(int projectId, int userId);
        Task<List<SupervisorProjectTimesheetPagingResponse>> SupervisorGetTimesheetPagingAsync(List<int> userIds, SupervisorProjectTimesheetPagingRequest request);
        Task<List<ChromeExtensionRawResponse>> GetTimesheetForExtensionByUserIdAndDateAsync(int userId, DateTime date);
        Task<List<ProjectTimeSheet>> GetRunningByUserIdAsync(int userId);
        Task<List<ProjectTimeSheet>> GetListByProjectIdAsync(int projectId);
        Task<List<ProjectTimeSheet>> GetByProjectIdStartDateAndEndDateAsync(int projectId, DateTime startDate, DateTime endDate);
    }
}
