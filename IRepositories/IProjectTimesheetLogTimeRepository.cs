using InternalPortal.ApplicationCore.Entities;
using InternalPortal.ApplicationCore.Models.ProjectManagement;

namespace InternalPortal.ApplicationCore.Interfaces.Repositories
{
    public interface IProjectTimesheetLogTimeRepository : IRepository<ProjectTimesheetLogTime>
    {
        Task<ProjectTimesheetLogTime> GetByLogworkId(string logworkId);
        Task<List<AzureDevOpsDataResponse>> GetLogTimeAzureAsync(string taskId, int projectId);
    }
}
