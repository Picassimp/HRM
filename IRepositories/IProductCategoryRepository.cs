using InternalPortal.ApplicationCore.Entities;
using InternalPortal.ApplicationCore.Models.Product;

namespace InternalPortal.ApplicationCore.Interfaces.Repositories
{
    public interface IProductCategoryRepository : IRepository<ProductCategory>
    {
        Task<List<ProductCategoryDropdownResponse>> GetDropdownAsync();
    }
}
