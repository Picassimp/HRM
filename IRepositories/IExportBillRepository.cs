using InternalPortal.ApplicationCore.Entities;
using InternalPortal.ApplicationCore.Models.ExportBill;
using InternalPortal.ApplicationCore.Models.PurchaseOrder.PurchaseRequest;

namespace InternalPortal.ApplicationCore.Interfaces.Repositories
{
    public interface IExportBillRepository : IRepository<ExportBill>
    {
        Task RemoveExportBillByPrIdAsync(int id);
        Task<List<ExportBillPagingResponseRaw>> GetAllWithPagingAsync(ExportBillPagingModel model);
        Task<List<ExportBillDetailResponseRaw>> GetExportBillDetailAsync(int exportBillId);
        Task<List<ExportBillLineItemResponseRaw>> GetExportBillLineItemAsync(int exportBillDetailId, int exportBillId);
        Task<List<ExportBillInfoModel>> GetDtoByLineItemIdsAsync(List<int> lineItemIds);
    }
}
