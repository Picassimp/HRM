using InternalPortal.ApplicationCore.Entities;
using InternalPortal.ApplicationCore.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace InternalPortal.Infrastructure.Persistence.Repositories
{
    public class UserDayOffSnapshotRepository : EfRepository<UserDayOffSnapShot>, IUserDayOffSnapshotRepository
    {
        public UserDayOffSnapshotRepository(ApplicationDbContext context) : base(context)
        {
        }

        public async Task<List<UserDayOffSnapShot>> GetByUserIdAndYearAsync(int userId, DateTime currentDate)
        {
            return await DbSet.Where(u => u.Month == 3 && u.Year == currentDate.Year && u.UserId == userId).ToListAsync();
        }
    }
}
