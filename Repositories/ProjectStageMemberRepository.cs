using InternalPortal.ApplicationCore.Entities;
using InternalPortal.ApplicationCore.Interfaces.Repositories;

namespace InternalPortal.Infrastructure.Persistence.Repositories
{
    public class ProjectStageMemberRepository : EfRepository<ProjectStageMember>, IProjectStageMemberRepository
    {
        public ProjectStageMemberRepository(ApplicationDbContext context) : base(context)
        {
        }
    }
}
