using InternalPortal.ApplicationCore.Entities;

namespace InternalPortal.ApplicationCore.Interfaces.Repositories
{
    public interface IProjectTimesheetEstimateRepository : IRepository<ProjectTimesheetEstimate>
    {
        Task<ProjectTimesheetEstimate?> GetByTaskIdAndProjectIdAsync(string taskId, int projectId); 
    }
}
