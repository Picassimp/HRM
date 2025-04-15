using InternalPortal.ApplicationCore.Entities;

namespace InternalPortal.ApplicationCore.Interfaces.Repositories
{
    public interface IRedemptionCodeRepository : IRepository<RedemptionCode>
    {
        Task<RedemptionCode?> GetCodeAsync();
    }
}
