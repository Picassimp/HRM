using InternalPortal.ApplicationCore.Entities;
using InternalPortal.ApplicationCore.Interfaces.Repositories;
using InternalPortal.ApplicationCore.Models.Inventory;
using Microsoft.EntityFrameworkCore;

namespace InternalPortal.Infrastructure.Persistence.Repositories
{
    public class InventoryRepository : EfRepository<Inventory>, IInventoryRepository
    {
        public InventoryRepository(ApplicationDbContext context) : base(context)
        {
        }

        public async Task<Inventory> GetByBarcodeAsync(string barcode)
        {
            return await DbSet.FirstOrDefaultAsync(p => p.Barcode == barcode);
        }

        public async Task<List<InventoryResponse>> GetPagingAsync(int pageIndex, int pageSize)
        {
            return await DbSet.OrderByDescending(p => p.Id).Skip(pageIndex * pageSize).Take(pageSize).Select(p => new InventoryResponse
            {
                Barcode = p.Barcode,
                Discount = p.Discount,
                DiscountedPrice = p.DiscountedPrice,
                Id = p.Id,
                ImageUrl = p.ImageUrl,
                Name = p.Name,
                Price = p.Price,
                Quantity = p.Quantity
            }).ToListAsync();
        }

        public async Task<int> GetTotalAsync()
        { 
            return await DbSet.CountAsync();
        }
    }
}
