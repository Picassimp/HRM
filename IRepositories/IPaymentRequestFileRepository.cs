using InternalPortal.ApplicationCore.Entities;

namespace InternalPortal.ApplicationCore.Interfaces.Repositories
{
    public interface IPaymentRequestFileRepository : IRepository<PaymentRequestFile>
    {
        Task<List<PaymentRequestFile>> GetByPaymentRequestIdAsync(int paymentRequestId);
    }
}
