using InternalPortal.ApplicationCore.Entities;

namespace InternalPortal.ApplicationCore.Interfaces.Repositories
{
    public interface IPOPRLineItemRepository : IRepository<PoprlineItem>
    {
        Task<List<PoprlineItem>> GetByPurchareOrderIdAsync(int id);
        Task<List<PoprlineItem>> GetByPurchaseOrderDetailIdAsync(int id);
    }
}
