using InternalPortal.ApplicationCore.Entities;
using InternalPortal.ApplicationCore.Interfaces.Repositories;

namespace InternalPortal.Infrastructure.Persistence.Repositories
{
    public class PurchaseOrderFileRepository : EfRepository<PurchaseOrderFile>, IPurchaseOrderFileRepository
    {
        public PurchaseOrderFileRepository(ApplicationDbContext context) : base(context)
        {
        }
    }
}
