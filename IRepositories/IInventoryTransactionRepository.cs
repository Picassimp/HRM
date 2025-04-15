using InternalPortal.ApplicationCore.Entities;
using InternalPortal.ApplicationCore.Models.Inventory;

namespace InternalPortal.ApplicationCore.Interfaces.Repositories
{
    public interface IInventoryTransactionRepository : IRepository<InventoryTransaction>
    {
        Task<List<TransactionResponse>> GetByUserIdAsync(int userId, int month, int year);
        Task<List<InventoryTransactionResponse>> GetAllByMonthYearAsync(int month, int year);
    }
}
