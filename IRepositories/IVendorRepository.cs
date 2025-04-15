using InternalPortal.ApplicationCore.Entities;

namespace InternalPortal.ApplicationCore.Interfaces.Repositories
{
    public interface IVendorRepository : IRepository<Vendor>
    {
        Task<Vendor> GetByNameAsync(string vendorName);
    }
}
