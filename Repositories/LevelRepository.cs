using InternalPortal.ApplicationCore.Entities;
using InternalPortal.ApplicationCore.Interfaces.Repositories;

namespace InternalPortal.Infrastructure.Persistence.Repositories
{
    public class LevelRepository : EfRepository<Level>, ILevelRepository
    {
        public LevelRepository(ApplicationDbContext context)
            : base(context)
        {
        }
    }
}
