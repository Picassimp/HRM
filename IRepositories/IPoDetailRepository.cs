using InternalPortal.ApplicationCore.Entities;

namespace InternalPortal.ApplicationCore.Interfaces.Repositories
{
    public interface IPODetailRepository : IRepository<Podetail>
    {
        Task<List<Podetail>> GetByPurchaseOrderIdAsync(int purchaseOrderId,bool? IsFromRequest);
    }
}
