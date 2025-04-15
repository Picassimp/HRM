using InternalPortal.ApplicationCore.Entities;
using InternalPortal.ApplicationCore.Interfaces.Repositories;

namespace InternalPortal.Infrastructure.Persistence.Repositories
{
    public class PaymentRequestProposeRepository : EfRepository<PaymentRequestPropose>, IPaymentRequestProposeRepository
    {
        public PaymentRequestProposeRepository(ApplicationDbContext context) : base(context)
        {
        }
    }
}
