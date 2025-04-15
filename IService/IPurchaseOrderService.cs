using InternalPortal.ApplicationCore.Entities;
using InternalPortal.ApplicationCore.Models;
using InternalPortal.ApplicationCore.Models.PurchaseOrder.Product;
using InternalPortal.ApplicationCore.Models.PurchaseOrder.PurchaseOrder;
using InternalPortal.ApplicationCore.Models.PurchaseOrder.PurchaseRequest;

namespace InternalPortal.ApplicationCore.Interfaces.Business
{
    public interface IPurchaseOrderService
    {
        Task<CombineResponseModel<PurchaseOrder>> CreatePOFromRequestAsync(int id,int userId,string email,AdditionalPurchaseOrderRequest requestPO);
        Task<CombineResponseModel<PurchaseOrderDetailResponse>> GetPurchaseOrderAsync(string email,int purchaseOrderId);   
        Task<CombineResponseModel<List<POPRLineItemResponse>>> GetPOPRLineItemAsync(string email,int purchaseOrderDetailId);
        Task<CombineResponseModel<PurchaseOrderFileResponse>> GetPurchaseOrderFileAsync(string email,int purchaseOrderId);
        Task<PagingResponseModel<PurchaseOrderPagingReponse>> GetPurchaseOrderPagingResponseAsync(PurchaseOrderPagingModel model);
        Task<CombineResponseModel<PurchaseOrder>> AddItemFromRequestAsync(int purchaseOrderId, ItemCreateRequest request);
        Task<CombineResponseModel<PurchaseOrder>> CreateAsync(int userId, string email, PurchaseOrderRequest request);
        Task<CombineResponseModel<PurchaseOrder>> UpdateAsync(int id,string email, PurchaseOrderRequest request);
        Task<CombineResponseModel<PurchaseOrder>> DeleteAsync(int id,string email);
        Task<CombineResponseModel<List<Podetail>>> AddNewProductAsync(int id,string email, ProductRequest request);
        Task<CombineResponseModel<List<PurchaseOrderFile>>> AddFileAsync(int id,string email, PurchaseOrderFileRequest request);
        Task<CombineResponseModel<PurchaseOrder>> ReceiveFullAsync(int id,string email);
        Task<CombineResponseModel<PurchaseOrder>> CreateCompensationPOAsync(int id, string email, int userId,AdditionalPurchaseOrderRequest request);
        Task<CombineResponseModel<PurchaseOrder>> ChangeStatusAsync(int id,string email,bool isPurchase);
        Task<CombineResponseModel<bool>> CheckFullReceiveAsync(int id,bool isSkip);
        Task<PagingResponseModel<PurchaseOrderPagingReponse>> AccountantGetPurchaseOrderPagingResponseAsync(PurchaseOrderPagingModel model);
        Task<CombineResponseModel<PurchaseOrder>> AccountantReviewAsync(string fullName,string email,int id,PurchaseOrderReviewModel model);
        Task<PagingResponseModel<PurchaseOrderPagingReponse>> DirectorGetPurchaseOrderPagingResponseAsync(PurchaseOrderPagingModel model);
        Task<CombineResponseModel<PurchaseOrder>> DirectorReviewAsync(string fullName, string email, int id, PurchaseOrderReviewModel model);
    }
}
