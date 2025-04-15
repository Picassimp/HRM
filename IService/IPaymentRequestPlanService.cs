using InternalPortal.ApplicationCore.Entities;
using InternalPortal.ApplicationCore.Models;
using InternalPortal.ApplicationCore.Models.PaymentRequest;

namespace InternalPortal.ApplicationCore.Interfaces.Business
{
    public interface IPaymentRequestPlanService
    {
        Task<CombineResponseModel<PaymentRequestPlan>> CreateAsync(int userId, int paymentRequestId, PaymentRequestPlanRequest request);
        Task<CombineResponseModel<PaymentRequestPlan>> DeleteAsync(int userId, int id);
        Task<CombineResponseModel<PaymentRequestPlan>> UpdateAsync(int userId, int id, PaymentRequestPlanRequest request);
        Task<CombineResponseModel<PaymentRequestPlan>> ChangeStatusAsync(int id, PaymentRequestPlanChangeStatusModel model, string email);
        Task<PagingResponseModel<PaymentRequestPlanPagingResponse>> GetAllWithPagingAsync(PaymentRequestPlanPagingModel model);
        Task<CombineResponseModel<PaymentRequestPlan>> AccountantUpdateAsync(string email, int id, PaymentRequestPlanRequest request);
        Task<CombineResponseModel<PaymentRequestPlan>> AccountantCreateAsync(string email, int paymentRequestId, PaymentRequestPlanRequest request);
        Task<CombineResponseModel<PaymentRequestPlan>> AccountantDeleteAsync(string email, int id);
        Task<CombineResponseModel<PaymentRequestPlan>> RefundAsync(string email, int id);
    }
}
