using InternalPortal.ApplicationCore.Entities;
using InternalPortal.ApplicationCore.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace InternalPortal.Infrastructure.Persistence.Repositories
{
    public class UserIPRepository : EfRepository<UserIp>, IUserIPRepository
    {
        public UserIPRepository(ApplicationDbContext context) : base(context)
        {

        }

        public async Task<UserIp?> GetbyIPAddressAsync(string ipAddress)
        {
            return await DbSet.FirstOrDefaultAsync(t => t.IpAddress == ipAddress);
        }

        public async Task<List<UserIp>> GetByUserIdAsync(int userId)
        {
            return await DbSet.Where(t => t.UserId == userId).ToListAsync();
        }
        public async Task<bool> CheckAnyIpAddressExist(List<string> ipAddress)
        {
            return await DbSet.AnyAsync(t => ipAddress.Contains(t.IpAddress));
        }
    }
}
