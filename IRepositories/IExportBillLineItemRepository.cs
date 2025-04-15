using InternalPortal.ApplicationCore.Entities;

namespace InternalPortal.ApplicationCore.Interfaces.Repositories
{
    public interface IExportBillLineItemRepository : IRepository<ExportBillLineItem>
    {
        Task<List<ExportBillLineItem>> GetByExportBillIdAsync(int exportBillId);
    }
}
