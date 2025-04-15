using InternalPortal.ApplicationCore.Entities;
using InternalPortal.ApplicationCore.Interfaces.Repositories;

namespace InternalPortal.Infrastructure.Persistence.Repositories
{
    public class UserClientRepository : EfRepository<UserClient>, IUserClientRepository
    {
        public UserClientRepository(ApplicationDbContext context) : base(context)
        {
        }
    }
}
