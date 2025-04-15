using InternalPortal.ApplicationCore.Entities;
using InternalPortal.ApplicationCore.Models.Pa;

namespace InternalPortal.ApplicationCore.Interfaces.Repositories
{
    public interface IPaRepository : IRepository<Pa>
    {
        Task<List<MyAnnualPaRaw>> GetMyAnnualAsync(int userId);
        Task<List<MyManualPaRaw>> GetMyManualAsync(int userId);
    }
}
