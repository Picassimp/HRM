using InternalPortal.ApplicationCore.Entities;
using InternalPortal.ApplicationCore.Models;
using InternalPortal.ApplicationCore.Models.PaymentRequest;

namespace InternalPortal.ApplicationCore.Interfaces.Business
{
    public interface IPaymentRequestService
    {
        Task<CombineResponseModel<PaymentRequest>> CreateAsync(PaymentRequestCreateModel model,int userId,string fullName);
        Task<CombineResponseModel<PaymentRequest>> UpdateAsync(PaymentRequestUpdateModel model,int userId, int id);
        Task<CombineResponseModel<PaymentRequest>> DeleteAsync(int id,int userId);
        Task<CombineResponseModel<List<PaymentRequestFile>>> AddFileAsync(int id, int userId,PaymentRequestFileRequest request);
        Task<PagingResponseModel<PaymentRequestPagingResponse>> GetUserPagingAsync(PaymentRequestPagingModel model, int userId);
        Task<PagingResponseModel<PaymentRequestPagingResponse>> GetManagerPagingAsync(PaymentRequestPagingModel model, int userId);
        Task<CombineResponseModel<PaymentRequest>> ManagerReviewAsync(int id,int userId, PaymentRequestReviewModel model);
        Task<CombineResponseModel<PaymentRequest>> AccountantReviewAsync(int id,PaymentRequestReviewModel model,string fullName,string email);
        Task<CombineResponseModel<PaymentRequest>> DirectorReviewAsync(int id, PaymentRequestReviewModel model, string fullName,string email);
        Task<PagingResponseModel<PaymentRequestPagingResponse>> GetAccountantPagingAsync(PaymentRequestPagingModel model);
        Task<PagingResponseModel<PaymentRequestPagingResponse>> GetDirectorPagingAsync(PaymentRequestPagingModel model);
        Task<CombineResponseModel<List<PaymentRequest>>> ManagerMultiReviewAsync(PaymentRequestMultiReviewModel model,int userId);
        Task<CombineResponseModel<List<PaymentRequest>>> AccountantMultiReviewAsync(PaymentRequestMultiReviewModel model, string email);
        Task<CombineResponseModel<List<PaymentRequest>>> DirectorMultiReviewAsync(PaymentRequestMultiReviewModel model, string email);
        Task SendMail(int id, string fullName);
        Task<CombineResponseModel<PaymentRequest>> AccountantUpdateRequestAsync(int id, string email, UpdateRequestModel model);
        Task<CombineResponseModel<PaymentRequest>> DirectorUpdateRequestAsync(int id, string email, UpdateRequestModel model);
        Task<CombineResponseModel<PaymentRequest>> AccountantResetAsync(int id, string email);
        Task<CombineResponseModel<PaymentRequest>> AccountantCancelAsync(int id, string email);
        Task<CombineResponseModel<PaymentRequest>> AccountantUpdateAsync(int id,string email, PaymentRequestAccountantUpdateModel model);
        Task<CombineResponseModel<PaymentRequest>> AccountantCloseAsync(int id, string email);
        Task<CombineResponseModel<PaymentRequest>> CopyAsync(int id, int userId);
    }
}
