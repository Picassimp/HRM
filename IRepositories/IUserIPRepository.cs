using InternalPortal.ApplicationCore.Entities;

namespace InternalPortal.ApplicationCore.Interfaces.Repositories
{
    public interface IUserIPRepository : IRepository<UserIp>
    {
        Task<List<UserIp>> GetByUserIdAsync(int userId);   
        Task<UserIp> GetbyIPAddressAsync(string ipAddress);
        Task<bool> CheckAnyIpAddressExist(List<string> ipAddress);
    }
}
