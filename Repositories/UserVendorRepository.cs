using InternalPortal.ApplicationCore.Entities;
using InternalPortal.ApplicationCore.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace InternalPortal.Infrastructure.Persistence.Repositories
{
    public class UserVendorRepository : EfRepository<UserVendor>, IUserVendorRepository
    {
        public UserVendorRepository(ApplicationDbContext context) : base(context)
        {
        }

        public async Task<List<UserVendor>> GetByUserIdAsync(int userId)
        {
            return await DbSet.Where(t => t.UserId == userId).ToListAsync();
        }
    }
}
