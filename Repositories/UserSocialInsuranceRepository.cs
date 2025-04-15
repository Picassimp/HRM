using InternalPortal.ApplicationCore.Entities;
using InternalPortal.ApplicationCore.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace InternalPortal.Infrastructure.Persistence.Repositories
{
    public class UserSocialInsuranceRepository : EfRepository<UserSocialInsurance>, IUserSocialInsuranceRepository
    {
        public UserSocialInsuranceRepository(ApplicationDbContext context) : base(context)
        {
        }
        public async Task<UserSocialInsurance> GetByUserIdFirstOrDefaultAsync(int userId)
        {
            return await DbSet.FirstOrDefaultAsync(o => o.UserId == userId);
        }
    }
}
