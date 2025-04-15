using InternalPortal.ApplicationCore.Entities;

namespace InternalPortal.ApplicationCore.Interfaces.Repositories
{
    public interface IProductKitRepository : IRepository<ProductKit>
    {
        Task<List<ProductKit>> GetByProductIdAsync(int productId);
    }
}
