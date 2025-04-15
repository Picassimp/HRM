using InternalPortal.ApplicationCore.Entities;
using InternalPortal.ApplicationCore.Models.PaymentPlan;

namespace InternalPortal.ApplicationCore.Interfaces.Repositories
{
    public interface IPaymentPlanRepository : IRepository<PaymentPlan>
    {
        Task<List<PaymentPlanPagingResponseRaw>> GetAllWithPagingAsync(PaymentPlanPagingModel model);
        Task<List<PaymentPlan>> GetByPoIdsAsync(List<int> poIds);
    }
}
