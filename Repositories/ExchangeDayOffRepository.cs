using InternalPortal.ApplicationCore.Entities;
using InternalPortal.ApplicationCore.Enums;
using InternalPortal.ApplicationCore.Interfaces.Repositories;
using InternalPortal.ApplicationCore.Models.User;
using Microsoft.EntityFrameworkCore;

namespace InternalPortal.Infrastructure.Persistence.Repositories
{
    public class ExchangeDayOffRepository : EfRepository<ExchangeDayOff>, IExchangeDayOffRepository
    {
        public ExchangeDayOffRepository(ApplicationDbContext context) : base(context)
        {
        }

        public async Task<bool> HasExchangeByUserIdAndYearAsync(int userId, int year)
        {
            return await DbSet.AnyAsync(o => o.UserId == userId && o.CreatedDate.Year == year && o.ReviewStatus != (int)EReviewStatus.Rejected);
        }

        public async Task<UserExchangeDayOffResponse?> GetByUserIdAsync(int userId, int year)
        {
            var dayOffExchange = await DbSet.SingleOrDefaultAsync(o => o.UserId == userId && o.CreatedDate.Year == year && o.ReviewStatus == (int)EReviewStatus.Reviewed);
            if (dayOffExchange != null)
            {
                return new UserExchangeDayOffResponse
                {
                    DayOffExchange = dayOffExchange.DayOffExchange
                };
            }

            return null;
        }

        public async Task<UserExchangeDayOffResponse?> GetPendingByUserIdAndYearAsync(int userId, int year)
        {
            var dayOffExchange = await DbSet.SingleOrDefaultAsync(o => o.UserId == userId && o.CreatedDate.Year == year && o.ReviewStatus == (int)EReviewStatus.Pending);
            if (dayOffExchange != null)
            {
                return new UserExchangeDayOffResponse
                {
                    DayOffExchange = dayOffExchange.DayOffExchange
                };
            }

            return null;
        }
    }
}
