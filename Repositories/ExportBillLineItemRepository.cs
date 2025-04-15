using InternalPortal.ApplicationCore.Entities;
using InternalPortal.ApplicationCore.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace InternalPortal.Infrastructure.Persistence.Repositories
{
    public class ExportBillLineItemRepository : EfRepository<ExportBillLineItem>, IExportBillLineItemRepository
    {
        public ExportBillLineItemRepository(ApplicationDbContext context) : base(context)
        {

        }

        public async Task<List<ExportBillLineItem>> GetByExportBillIdAsync(int exportBillId)
        {
            return await DbSet.Where(t => t.ExportBillDetail.ExportBillId == exportBillId).ToListAsync();
        }
    }
}
