using InternalPortal.ApplicationCore.Entities;
using InternalPortal.ApplicationCore.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace InternalPortal.Infrastructure.Persistence.Repositories
{
    public class PaymentRequestFileRepository : EfRepository<PaymentRequestFile>, IPaymentRequestFileRepository
    {
        public PaymentRequestFileRepository(ApplicationDbContext context) : base(context)
        {

        }

        public async Task<List<PaymentRequestFile>> GetByPaymentRequestIdAsync(int paymentRequestId)
        {
            return await DbSet.Where(t => t.PaymentRequestId == paymentRequestId).ToListAsync();
        }
    }
}
