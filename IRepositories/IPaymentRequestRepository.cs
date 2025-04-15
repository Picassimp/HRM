using InternalPortal.ApplicationCore.Entities;
using InternalPortal.ApplicationCore.Models.PaymentRequest;

namespace InternalPortal.ApplicationCore.Interfaces.Repositories
{
    public interface IPaymentRequestRepository : IRepository<PaymentRequest>
    {
        Task<List<PaymentRequestPagingResponseRaw>> GetUserPagingAsync(PaymentRequestPagingModel model,int userId);
        Task<List<PaymentRequestPagingResponseRaw>> GetManagerPagingAsync(PaymentRequestPagingModel model, int userId);
        Task<List<PaymentRequestPagingResponseRaw>> GetAccountantPagingAsync(PaymentRequestPagingModel model);
        Task<List<PaymentRequestPagingResponseRaw>> GetDirectorPagingAsync(PaymentRequestPagingModel model);
        Task<List<PaymentRequest>> GetMultiRequestAsync(List<int> paymentRequestIds);  
    }
}
