using InternalPortal.ApplicationCore.Entities;
using InternalPortal.ApplicationCore.Interfaces.Repositories;
using SkiaSharp;

namespace InternalPortal.Infrastructure.Persistence.Repositories
{
    public class PaScoreRepository : EfRepository<PaScore>, IPaScoreRepository
    {
        public PaScoreRepository(ApplicationDbContext context) : base(context)
        {
        }
    }
}
