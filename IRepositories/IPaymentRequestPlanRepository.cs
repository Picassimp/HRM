using InternalPortal.ApplicationCore.Entities;
using InternalPortal.ApplicationCore.Models.PaymentRequest;

namespace InternalPortal.ApplicationCore.Interfaces.Repositories
{
    public interface IPaymentRequestPlanRepository : IRepository<PaymentRequestPlan>
    {
        Task<List<PaymentRequestPlanPagingResponseRaw>> GetPagingAsync(PaymentRequestPlanPagingModel model);
        Task<List<PaymentRequestPlan>> GetByPaymentRequestIdAsync(int paymentRequestId);    
    }
}
