using InternalPortal.ApplicationCore.Entities;
using InternalPortal.ApplicationCore.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace InternalPortal.Infrastructure.Persistence.Repositories
{
    public class ProjectTimesheetEstimateRepository : EfRepository<ProjectTimesheetEstimate>, IProjectTimesheetEstimateRepository
    {
        public ProjectTimesheetEstimateRepository(ApplicationDbContext context) : base(context)
        {
        }

        public Task<ProjectTimesheetEstimate?> GetByTaskIdAndProjectIdAsync(string taskId, int projectId)
        {
            return DbSet.FirstOrDefaultAsync(o => o.TaskId.ToLower() == taskId.ToLower() && o.ProjectId == projectId);
        }
    }
}
