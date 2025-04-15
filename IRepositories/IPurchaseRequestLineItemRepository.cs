using InternalPortal.ApplicationCore.Entities;
using InternalPortal.ApplicationCore.Models.PurchaseOrder.PurchaseRequest;

namespace InternalPortal.ApplicationCore.Interfaces.Repositories
{
    public interface IPurchaseRequestLineItemRepository : IRepository<PurchaseRequestLineItem>
    {
        Task<List<ItemCreateResponse>> GetItemCreateResponse(string purchaseRequestLineItemIds,int purchaseOrderId);
        Task<List<PurchaseRequestLineItemResponseRaw>> GetPurchaseRequestLineItemAsync(int purchaseRequestId, int purchaseOrderId, int exportBillId);
        Task<List<PurchaseRequestLineItem>> GetByPurchaseRequestIdAsync(int purchaseRequestId);
        Task<List<ItemCreateResponse>> GetItemCreateForEBResponse(string purchaseRequestLineItemIds, int exportBillId);
    }
}