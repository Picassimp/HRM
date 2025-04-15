using InternalPortal.ApplicationCore.Entities;
using InternalPortal.ApplicationCore.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace InternalPortal.Infrastructure.Persistence.Repositories
{
    public class PaymentRequestDetailRepository : EfRepository<PaymentRequestDetail>, IPaymentRequestDetailRepository
    {
        public PaymentRequestDetailRepository(ApplicationDbContext context) : base(context)
        {
        }

        public async Task<List<PaymentRequestDetail>> GetByPaymentRequestIdAsync(int paymentRequestId)
        {
            return await DbSet.Where(t => t.PaymentRequestId == paymentRequestId).ToListAsync();
        }
    }
}
