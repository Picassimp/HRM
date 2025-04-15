using InternalPortal.ApplicationCore.Entities;
using InternalPortal.ApplicationCore.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace InternalPortal.Infrastructure.Persistence.Repositories
{
    public class VendorRepository : EfRepository<Vendor>, IVendorRepository
    {
        public VendorRepository(ApplicationDbContext context) : base(context)
        {

        }

        public async Task<Vendor> GetByNameAsync(string vendorName)
        {
            return await DbSet.FirstOrDefaultAsync(t=>t.VendorName == vendorName);
        }
    }
}
