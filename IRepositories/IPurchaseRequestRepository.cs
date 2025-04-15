using InternalPortal.ApplicationCore.Entities;
using InternalPortal.ApplicationCore.Enums;
using InternalPortal.ApplicationCore.Models.PurchaseOrder;
using InternalPortal.ApplicationCore.Models.PurchaseOrder.PurchaseRequest;

namespace InternalPortal.ApplicationCore.Interfaces.Repositories
{
    public interface IPurchaseRequestRepository : IRepository<PurchaseRequest>
    {
        Task<int?> IsValidMemberAsync(int projectId, int userId);
        Task<List<PurchaseRequestPagingResponse>> GetAllWithPagingAsync(PurchaseRequestPagingModel request, int userId);
        Task<List<PurchaseRequestManagerPagingResponse>> GetAllWithPagingByManagerAsync(int managerId, PurchaseRequestManagerPagingRequest request);
        Task<List<AdminPurchaseRequestResponseRawModel>> GetPrDtoByPrIdAsync(int id);
        Task<List<AccountantPurchaseRequestPagingRawResponse>> AccountantGetPrPagingAsync(AccountantPurchaseRequestPagingModel request, bool isDirector);
        Task RemovePoByPrIdAsync(int purchaseRequestId);
        Task<List<HRPurchaseRequestPagingResponseRaw>> HRGetAllWithPagingAsync(HRPurchaseRequestPagingRequest model);
        Task<List<PurchaseRequestFileResponseRaw>> GetPurchaseRequestFileAsync(int purchaseOrderId);
        Task<List<PuchaseRequestDropdown>> GetPurchaseRequestsAsync(int purchaseOrderId,bool isCompensationPO);
        Task<List<PuchaseRequestDropdown>> GetPurchaseRequestsForEBAsync(int exportBillId);
        Task<List<MultiplePurchaseRequestDtoModel>> GetByIdsAsync(List<int> purchaseRequestIds);
        Task AdminReviewPurchaseRequestsAsync(List<int> purchaseRequestIds, EPurchaseRequestStatus reviewStatus);
        Task UpdatePurchaseRequestAsync(int prId, string? reviewNote, EPurchaseRequestStatus reviewStatus);
    }
}