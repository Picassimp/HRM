using InternalPortal.ApplicationCore.Entities;
using InternalPortal.ApplicationCore.Models.Inventory;

namespace InternalPortal.ApplicationCore.Interfaces.Repositories
{
    public interface IInventoryRepository : IRepository<Inventory>
    {
        Task<Inventory> GetByBarcodeAsync(string barcode);
        Task<int> GetTotalAsync();
        Task<List<InventoryResponse>> GetPagingAsync(int pageIndex, int pageSize);
    }
}
