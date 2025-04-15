using InternalPortal.ApplicationCore.Entities;
using InternalPortal.ApplicationCore.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace InternalPortal.Infrastructure.Persistence.Repositories
{
    public class ProductKitRepository : EfRepository<ProductKit>, IProductKitRepository
    {
        public ProductKitRepository(ApplicationDbContext context) : base(context)
        {
        }

        public async Task<List<ProductKit>> GetByProductIdAsync(int productId)
        {
            return await DbSet.Where(t => t.ProductId == productId).ToListAsync();
        }
    }
}
