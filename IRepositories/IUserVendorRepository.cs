using InternalPortal.ApplicationCore.Entities;

namespace InternalPortal.ApplicationCore.Interfaces.Repositories
{
    public interface IUserVendorRepository : IRepository<UserVendor>
    {
        Task<List<UserVendor>> GetByUserIdAsync(int userId);
    }
}
