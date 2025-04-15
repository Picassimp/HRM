using InternalPortal.ApplicationCore.Entities;
using InternalPortal.ApplicationCore.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace InternalPortal.Infrastructure.Persistence.Repositories
{
    public class ProjectTimesheetTagRepository : EfRepository<ProjectTimesheetTag>, IProjectTimesheetTagRepository
    {
        public ProjectTimesheetTagRepository(ApplicationDbContext context) : base(context)
        {
        }

        public async Task<List<ProjectTimesheetTag>> GetByProjectTimesheetIdsAsync(List<int> ids)
        {
            return await DbSet.Where(o => ids.Contains(o.ProjectTimesheetId)).ToListAsync();
        }
    }
}
