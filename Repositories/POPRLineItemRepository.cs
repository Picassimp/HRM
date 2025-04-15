using InternalPortal.ApplicationCore.Entities;
using InternalPortal.ApplicationCore.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace InternalPortal.Infrastructure.Persistence.Repositories
{
    public class POPRLineItemRepository : EfRepository<PoprlineItem>, IPOPRLineItemRepository
    {
        public POPRLineItemRepository(ApplicationDbContext context) : base(context)
        {
        }

        public async Task<List<PoprlineItem>> GetByPurchareOrderIdAsync(int id)
        {
           return await DbSet.Where(t => t.PurchaseOrderDetail.PurchaseOrderId == id).ToListAsync();
        }

        public async Task<List<PoprlineItem>> GetByPurchaseOrderDetailIdAsync(int id)
        {
            return await DbSet.Where(t=>t.PurchaseOrderDetailId == id).ToListAsync();
        }
    }
}
