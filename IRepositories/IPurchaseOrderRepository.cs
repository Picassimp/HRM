using InternalPortal.ApplicationCore.Entities;
using InternalPortal.ApplicationCore.Models.PurchaseOrder.PurchaseRequest;
using InternalPortal.ApplicationCore.Models.PurchaseOrder.PurchaseOrder;

namespace InternalPortal.ApplicationCore.Interfaces.Repositories
{
    public interface IPurchaseOrderRepository : IRepository<PurchaseOrder>
    {
        Task<List<PurchaseOrderDtoModel>> GetPoDtoByPrIdsAsync(List<int> prIds);
        Task<List<PurchaseOrderProductDetailRaw>> GetPurchaseOrderDetailAsync(int purchaseOrderId);
        Task<List<POPRLineItemResponseRaw>> GetPOPRLineItemAsync (int purchaseOrderDetailId,int purchaseOrderId);
        Task<List<PurchaseOrderPagingReponseRaw>> GetPurchaseOrderPagingAsync(PurchaseOrderPagingModel model);
        Task<List<PurchaseOrderTotalPriceModel>> GetPurchaseOrderTotalPriceAsync(int purchaseOrderId);
        Task<List<PurchaseOrderValidateModel>> GetPoPriceByIdsAsync(List<int> poIds);
        Task UpdateStatusAsync(List<int> poIds);
        Task<List<ExportBillDtoModel>> GetEbDtoByPrIdsAsync(List<int> prIds);
    }
}
