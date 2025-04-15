using InternalPortal.ApplicationCore.Entities;

namespace InternalPortal.ApplicationCore.Interfaces.Repositories
{
    public interface IPoLineItemRepository : IRepository<PoprlineItem>
    {
        Task<List<PoprlineItem>> GetByPrLineItemIdsAsync(List<int> prLineItemIds);
    }
}
