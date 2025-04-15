using InternalPortal.ApplicationCore.Entities;
using InternalPortal.ApplicationCore.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace InternalPortal.Infrastructure.Persistence.Repositories
{
    public class ProjectIssueTypeRepository : EfRepository<ProjectIssueType>, IProjectIssueTypeRepository
    {
        public ProjectIssueTypeRepository(ApplicationDbContext context) : base(context)
        {
        }

        public async Task<List<ProjectIssueType>> GetListByProjectIdAsync(int projectId)
        {
            return await DbSet.Where(o => o.ProjectId == projectId).ToListAsync();
        }

        public async Task<List<ProjectIssueType>> GetByProjectIdsAsync(List<int> projectIds)
        {
            return await DbSet.Where(o => projectIds.Contains(o.ProjectId)).ToListAsync();
        }
    }
}
