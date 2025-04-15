using InternalPortal.ApplicationCore.Entities;
using InternalPortal.ApplicationCore.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace InternalPortal.Infrastructure.Persistence.Repositories
{
    public class PoLineItemRepository : EfRepository<PoprlineItem>, IPoLineItemRepository
    {
        public PoLineItemRepository(ApplicationDbContext context) : base(context)
        {
        }

        public async Task<List<PoprlineItem>> GetByPrLineItemIdsAsync(List<int> prLineItemIds)
        {
            return await DbSet.Where(o => prLineItemIds.Contains(o.PorequestLineItemId)).ToListAsync();
        }
    }
}
