using InternalPortal.ApplicationCore.Entities;
using InternalPortal.ApplicationCore.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace InternalPortal.Infrastructure.Persistence.Repositories
{
    public class PODetailRepository : EfRepository<Podetail>, IPODetailRepository
    {
        public PODetailRepository(ApplicationDbContext context) : base(context)
        {
        }

        public async Task<List<Podetail>> GetByPurchaseOrderIdAsync(int purchaseOrderId,bool? IsFromRequest)
        {
            var query = DbSet.Where(t => t.PurchaseOrderId == purchaseOrderId);
            if (IsFromRequest.HasValue)
            {
                query = query.Where(t => t.IsFromRequest == IsFromRequest.Value);
            }
            return await query.ToListAsync();
        }
    }
}
