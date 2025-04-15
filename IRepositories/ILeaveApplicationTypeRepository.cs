using InternalPortal.ApplicationCore.Entities;

namespace InternalPortal.ApplicationCore.Interfaces.Repositories
{
    public interface ILeaveApplicationTypeRepository : IRepository<LeaveApplicationType>
    {
        Task<List<LeaveApplicationType>> GetByGroupUserIdAsync(int groupUserId);
    }
}
