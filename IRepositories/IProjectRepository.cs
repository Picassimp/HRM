using InternalPortal.ApplicationCore.Entities;
using InternalPortal.ApplicationCore.Models.ProjectManagement;
using InternalPortal.ApplicationCore.Models.ProjectManagement.SyncIssueTypeModel;
using InternalPortal.ApplicationCore.Models.PurchaseOrder;

namespace InternalPortal.ApplicationCore.Interfaces.Repositories
{
    public interface IProjectRepository : IRepository<Project> 
    {
        Task<List<ProjectDataModel>> GetProjectDataFilterAsync(int userId);
        Task<List<ProjectDataModel>> GetAllProjectsByUserIdAsync(int userId);
        Task<List<UserDataModel>> GetUserDataFilterAsync(int userId);
        Task<List<DataForFilterLinkingResponse>> ManagerGetDataForFilterLinkingAsync(int userId);
        Task<List<ProjectReportRawResponse>> ManagerGetReportDataAsync(int managerId, ManagerProjectReportRequest request);
        Task<List<ProjectReportRawResponse>> ManagerGetChartDataAsync(int managerId, ManagerProjectChartRequest request);
        Task<List<ProjectDetailForReportingRaw>> ManagerGetDataDetailForReportingAsync(int managerId, ManagerProjectDetailRequest request);
        Task<List<ProjectModel>> GetProjectsAsync(int userId);
        Task<List<Project>> GetExistingProjectAsync(ProjectCreateRequest model, int userId);
        Task<List<Project>> GetListProjectAsync();
        Task<List<Project>> GetListProjectAsync(int clientId);
        Task<List<CommonUserProjectsRaw>> UserGetProjectAsync(int userId);
        Task<List<ProjectSyncIssueTypeResponse>> GetForSyncIssueTypeAsync();
        Task<Project?> GetByAzureDevOpsProjectIdAsync(string azureDevOpsProjectId, string projectId);
        Task<List<PrProjectDropDownResponseModel>> GetByUserIdsAsync(string? userIds);
        Task<Project?> GetByAzureDevOpsOrganizationAsync(string azureDevOpsOrganization);
        Task<Project?> GetByProjectNameAndClientIdAsync(string projectName, int clientId);
        Task<decimal?> CalculateTotalWorkingHourByProjectIdAsync(int projectId);
    }
}
