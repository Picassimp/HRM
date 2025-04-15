using InternalPortal.ApplicationCore.Entities;
using InternalPortal.ApplicationCore.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace InternalPortal.Infrastructure.Persistence.Repositories
{
    public class AccessControlLogRepository : EfRepository<AccessControlLog>, IAccessControlLogRepository
    {
        public AccessControlLogRepository(ApplicationDbContext context) : base(context)
        {
        }

        public async Task<bool> IsHaveAnyCheckInAsync(int userId, DateTime checkInDate)
        {
            return await DbSet.AnyAsync(o => o.UserId == userId && o.CheckIn.HasValue && o.CheckIn.Value.Date == checkInDate);
        }
    }
}
