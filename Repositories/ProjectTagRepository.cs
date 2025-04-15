using InternalPortal.ApplicationCore.Entities;
using InternalPortal.ApplicationCore.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace InternalPortal.Infrastructure.Persistence.Repositories
{
    public class ProjectTagRepository : EfRepository<ProjectTag>, IProjectTagRepository
    {
        public ProjectTagRepository(ApplicationDbContext context) : base(context)
        {
        }

        public async Task<List<ProjectTag>> GetListByProjectIdAsync(int projectId)
        {
            return await DbSet.Where(o => o.ProjectId == projectId).ToListAsync();
        }
        public async Task<List<ProjectTag>> GetByProjectIdsAsync(List<int> projectIds)
        {
            return await DbSet.Where(o => projectIds.Contains(o.ProjectId)).ToListAsync();
        }
    }
}
