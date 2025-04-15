using InternalPortal.ApplicationCore.Entities;

namespace InternalPortal.ApplicationCore.Interfaces.Repositories
{
    public interface IExportBillDetailRepository : IRepository<ExportBillDetail>
    {
        Task<List<ExportBillDetail>> GetByExportBillIdAsync(int exportBillId);
    }
}
