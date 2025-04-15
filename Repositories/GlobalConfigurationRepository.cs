using InternalPortal.ApplicationCore.Entities;
using InternalPortal.ApplicationCore.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace InternalPortal.Infrastructure.Persistence.Repositories
{
    public class GlobalConfigurationRepository : EfRepository<GlobalConfiguration>, IGlobalConfigurationRepository
    {
        public GlobalConfigurationRepository(ApplicationDbContext context) : base(context)
        {
        }

        public async Task<GlobalConfiguration?> GetByNameAsync(string name)
        {
            return await DbSet.FirstOrDefaultAsync(o => o.Name == name);
        }
        public async Task<List<GlobalConfiguration>> GetByMultiNameAsync(List<string> names)
        {
            return await DbSet.Where(t=>names.Contains(t.Name)).ToListAsync();
        }
    }
}
