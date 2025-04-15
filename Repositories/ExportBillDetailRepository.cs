using InternalPortal.ApplicationCore.Entities;
using InternalPortal.ApplicationCore.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace InternalPortal.Infrastructure.Persistence.Repositories
{
    public class ExportBillDetailRepository : EfRepository<ExportBillDetail>, IExportBillDetailRepository
    {
        public ExportBillDetailRepository(ApplicationDbContext context) : base(context)
        {

        }

        public async Task<List<ExportBillDetail>> GetByExportBillIdAsync(int exportBillId)
        {
            return await DbSet.Where(t=>t.ExportBillId == exportBillId).ToListAsync();
        }
    }
}
