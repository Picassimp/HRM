using InternalPortal.ApplicationCore.Entities;
using InternalPortal.ApplicationCore.Interfaces.Repositories;

namespace InternalPortal.Infrastructure.Persistence.Repositories
{
    public class OnsiteApplicationFileRepository : EfRepository<OnsiteApplicationFile>, IOnsiteApplicationFileRepository
    {
        public OnsiteApplicationFileRepository(ApplicationDbContext context) : base(context)
        {
        }
    }
}
