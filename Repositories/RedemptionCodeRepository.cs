using InternalPortal.ApplicationCore.Entities;
using InternalPortal.ApplicationCore.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace InternalPortal.Infrastructure.Persistence.Repositories
{
    public class RedemptionCodeRepository : EfRepository<RedemptionCode>, IRedemptionCodeRepository
    {
        public RedemptionCodeRepository(ApplicationDbContext context) : base(context)
        {
        }

        public async Task<RedemptionCode?> GetCodeAsync()
        {
            return await DbSet.FirstOrDefaultAsync(p => !p.IssuedDate.HasValue);
        }
    }
}
