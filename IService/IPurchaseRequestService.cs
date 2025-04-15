using InternalPortal.ApplicationCore.Entities;
using InternalPortal.ApplicationCore.Models;
using InternalPortal.ApplicationCore.Models.PurchaseOrder;
using InternalPortal.ApplicationCore.Models.PurchaseOrder.Director;
using InternalPortal.ApplicationCore.Models.PurchaseOrder.Hr;
using InternalPortal.ApplicationCore.Models.PurchaseOrder.PurchaseRequest;
using InternalPortal.ApplicationCore.Models.User;

namespace InternalPortal.ApplicationCore.Interfaces.Business
{
    public interface IPurchaseRequestService
    {
        Task<PagingResponseModel<PurchaseRequestPagingResponse>> GetAllWithPagingAsync(PurchaseRequestPagingModel request, int userId);
        Task<PagingResponseModel<PurchaseRequestManagerPagingResponse>> GetAllWithPagingByManagerAsync(int managerId, PurchaseRequestManagerPagingRequest request);
        Task<CombineResponseModel<PurchaseRequestResponseModel>> GetByUserIdAndPrIdAsync(int userId, int id);
        Task<CombineResponseModel<PurchaseRequestResponseModel>> GetByManagerIdAndPrIdAsync(int managerId, int id);
        Task<CombineResponseModel<int>> PrepareCreateAsync(UserDtoModel user, PurchaseRequestPermissionModel permission, PurchaseRequestCreateModel request);
        Task<CombineResponseModel<int>> PrepareUpdateAsync(int id, UserDtoModel user, PurchaseRequestPermissionModel permission, PurchaseRequestUpdateModel request);
        Task<CombineResponseModel<int>> PrepareReviewAsync(int id, UserDtoModel user, PurchaseRequestManagerReviewModel request);
        Task<CombineResponseModel<int>> PrepareUpdateRequestAsync(int id, UserDtoModel user, PurchaseRequestManagerUpdateRequestModel request);
        Task<CombineResponseModel<int>> PrepareCancelledAsync(int id, int userId);
        Task SendEmailAsync(int purchaseRequestId);
        Task SendEmailFromManagerAsync(int purchaseRequestId);
        Task<CombineResponseModel<AdminResponseModel>> GetPrDtoByPrIdAsync(int id);
        Task<CombineResponseModel<int>> HrPrepareReviewAsync(int id, HrReviewModel request);
        Task HrSendEmailAsync(int purchaseRequestId);
        Task<PagingResponseModel<AccountantPurchaseRequestPagingResponse>> AccountantGetPrPagingAsync(AccountantPurchaseRequestPagingModel request, bool isDirector);
        Task<CombineResponseModel<int>> AccountantPrepareReviewAsync(int id, AccountantReviewModel request);
        Task AccountantSendEmailAsync(string accountantName, int purchaseRequestId);
        Task<CombineResponseModel<int>> DirectorPrepareReviewAsync(int id, DirectorReviewModel request);
        Task DirectorSendEmailAsync(string directorName, int purchaseRequestId);
        Task<PagingResponseModel<HRPurchaseRequestPagingResponse>> HRGetAllWithPagingAsync(HRPurchaseRequestPagingRequest model);
        Task<CombineResponseModel<bool>> PrepareDeleteAsync(int id, int userId);
        Task<CombineResponseModel<List<int>>> AdminPrepareReviewAsync(AdminMultipleRequestReviewModel request);
        Task<CombineResponseModel<PurchaseRequest>> ChangeStatusAsync(int id);
    }
}
