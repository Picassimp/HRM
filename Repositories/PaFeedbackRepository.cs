using InternalPortal.ApplicationCore.Entities;
using InternalPortal.ApplicationCore.Interfaces.Repositories;
using SkiaSharp;

namespace InternalPortal.Infrastructure.Persistence.Repositories
{
    public class PaFeedbackRepository : EfRepository<PaFeedback>, IPaFeedbackRepository
    {
        public PaFeedbackRepository(ApplicationDbContext context) : base(context)
        {
        }
    }
}
