using InternalPortal.ApplicationCore.Models.PurchaseOrder.PurchaseRequest;

namespace InternalPortal.ApplicationCore.Interfaces.Business
{
    public interface IPurchaseRequestLineItemService
    {
        Task<List<PurchaseRequestLineItemResponse>> GetPurchaseRequestLineItemAsync(int purchaseRequestId, int purchaseOrderId, int exportBillId);
    }
}
