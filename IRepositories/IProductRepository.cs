using InternalPortal.ApplicationCore.Entities;
using InternalPortal.ApplicationCore.Models.Product;

namespace InternalPortal.ApplicationCore.Interfaces.Repositories
{
    public interface IProductRepository : IRepository<Product>
    {
        Task<List<Product>> GetByIdsAsync(List<int> ids);
        Task<List<ProductDropdownResponse>> GetDropdownAsync();
        Task<List<SubProductDtoModel>> GetSubProductsByProductIdsAsync(List<int> productIds);
    }
}
