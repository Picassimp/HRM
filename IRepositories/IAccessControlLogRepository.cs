using InternalPortal.ApplicationCore.Entities;

namespace InternalPortal.ApplicationCore.Interfaces.Repositories
{
    public interface IAccessControlLogRepository : IRepository<AccessControlLog>
    {
        Task<bool> IsHaveAnyCheckInAsync(int userId, DateTime checkInDate);
    }
}
