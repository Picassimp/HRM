using InternalPortal.ApplicationCore.Entities;

namespace InternalPortal.ApplicationCore.Interfaces.Repositories
{
    public interface IUserSocialInsuranceRepository : IRepository<UserSocialInsurance>
    {
        Task<UserSocialInsurance> GetByUserIdFirstOrDefaultAsync(int userId);
    }
}
