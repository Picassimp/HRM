using InternalPortal.ApplicationCore.Entities;

namespace InternalPortal.ApplicationCore.Interfaces.Repositories
{
    public interface IPaymentRequestDetailRepository : IRepository<PaymentRequestDetail>
    {
        Task<List<PaymentRequestDetail>> GetByPaymentRequestIdAsync(int paymentRequestId);
    }
}
