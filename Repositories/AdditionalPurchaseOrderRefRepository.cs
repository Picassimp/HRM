using InternalPortal.ApplicationCore.Entities;
using InternalPortal.ApplicationCore.Interfaces.Repositories;

namespace InternalPortal.Infrastructure.Persistence.Repositories
{
    public class AdditionalPurchaseOrderRefRepository : EfRepository<AdditionalPurchaseOrderRef>,IAdditionalPurchaseOrderRefRepository
    {
        public AdditionalPurchaseOrderRefRepository(ApplicationDbContext context) : base(context)
        {
        }
    }
}
