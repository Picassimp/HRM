using InternalPortal.ApplicationCore.Entities;
using InternalPortal.ApplicationCore.Interfaces.Repositories;
using SkiaSharp;

namespace InternalPortal.Infrastructure.Persistence.Repositories
{
    public class PaProjectRepository : EfRepository<PaProject>, IPaProjectRepository
    {
        public PaProjectRepository(ApplicationDbContext context) : base(context)
        {
        }
    }
}
