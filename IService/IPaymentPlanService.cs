using InternalPortal.ApplicationCore.Entities;
using InternalPortal.ApplicationCore.Models;
using InternalPortal.ApplicationCore.Models.PaymentPlan;

namespace InternalPortal.ApplicationCore.Interfaces.Business
{
    public interface IPaymentPlanService
    {
        Task<CombineResponseModel<PaymentPlan>> CreateAsync(int id, PaymentPlanRequest request);
        Task<CombineResponseModel<PaymentPlan>> DeleteAsync(int id);
        Task<CombineResponseModel<PaymentPlan>> UpdateAsync(int id, PaymentPlanRequest request);
        Task<PagingResponseModel<PaymentPlanPagingResponse>> GetAllWithPagingAsync(PaymentPlanPagingModel model);
        Task<CombineResponseModel<PaymentPlan>> ChangeStatusAsync(int id);
        Task<CombineResponseModel<PaymentPlan>> RefundAsync(int id, PaymentRefundRequest request);
    }
}
