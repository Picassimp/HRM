using InternalPortal.ApplicationCore.Entities;
using InternalPortal.ApplicationCore.Interfaces.Repositories;
using InternalPortal.ApplicationCore.Models.Product;
using Microsoft.EntityFrameworkCore;

namespace InternalPortal.Infrastructure.Persistence.Repositories
{
    public class ProductCategoryRepository : EfRepository<ProductCategory>, IProductCategoryRepository
    {
        public ProductCategoryRepository(ApplicationDbContext context) : base(context)
        {
        }

        public async Task<List<ProductCategoryDropdownResponse>> GetDropdownAsync()
        {
            return await DbSet.Select(o => new ProductCategoryDropdownResponse
            {
                Id = o.Id,
                Name = o.Name,
            }).OrderBy(o => o.Name).ToListAsync();
        }
    }
}