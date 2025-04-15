using InternalPortal.ApplicationCore.Entities;

namespace InternalPortal.ApplicationCore.Interfaces.Repositories
{
    public interface IUserContractRepository : IRepository<UserContract>
    {
        Task<List<UserContract>> GetByUserIdAsync(int userId);
    }
}
