using InternalPortal.ApplicationCore.Entities;
using InternalPortal.ApplicationCore.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace InternalPortal.Infrastructure.Persistence.Repositories
{
    public class UserContractRepository : EfRepository<UserContract>, IUserContractRepository
    {
        public UserContractRepository(ApplicationDbContext context) : base(context)
        {
        }
        public async Task<List<UserContract>> GetByUserIdAsync(int userId)
        {
            return await DbSet.Where(o => o.UserId == userId).ToListAsync();
        }
    }
}
