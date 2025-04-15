using InternalPortal.ApplicationCore.Entities;
using InternalPortal.ApplicationCore.Interfaces.Repositories;

namespace InternalPortal.Infrastructure.Persistence.Repositories
{
    public class GroupUserRepository : EfRepository<GroupUser>, IGroupUserRepository
    {
        public GroupUserRepository(ApplicationDbContext context) : base(context)
        {
        }
    }
}
