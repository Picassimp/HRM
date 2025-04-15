using InternalPortal.ApplicationCore.Entities;
using InternalPortal.ApplicationCore.Models.User;

namespace InternalPortal.ApplicationCore.Interfaces.Repositories
{
    public interface IExchangeDayOffRepository : IRepository<ExchangeDayOff>
    {
        Task<bool> HasExchangeByUserIdAndYearAsync(int userId, int year);
        Task<UserExchangeDayOffResponse?> GetByUserIdAsync(int userId, int year);
        Task<UserExchangeDayOffResponse?> GetPendingByUserIdAndYearAsync(int userId, int year);
    }
}
