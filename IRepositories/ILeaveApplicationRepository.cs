using InternalPortal.ApplicationCore.Entities;
using InternalPortal.ApplicationCore.Models.LeaveApplication;

namespace InternalPortal.ApplicationCore.Interfaces.Repositories
{
    public interface ILeaveApplicationRepository : IRepository<LeaveApplication>
    {
        Task<List<LeaveApplicationStatusModel>> GetByUserIdAndDateAsync(int userId, DateTime fromDate, DateTime toDate);
        Task<List<LeaveApplication>> GetPendingByUserIdAsync(int userId);
        Task<List<LeaveApplication>> GetPendingSickByUserIdAsync(int userId);
        Task<List<LeaveApplicationPagingRawModel>> GetAllWithPagingAsync(LeaveApplicationPagingRequest request, int userId);
        Task<List<LeaveApplicationPagingMobileModelRaw>> GetAllWithPagingMobileAsync(LeaveApplicationSearchMobileModel searchModel, int userId);
    }
}
